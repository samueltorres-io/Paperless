#!/usr/bin/env bash
#
# Paperless installer — Linux & macOS
#
# Uso:
#     curl -fsSL https://github.com/SEU_USUARIO/paperless/releases/latest/download/install.sh | bash
#
# Variáveis opcionais:
#     PAPERLESS_INSTALL_DIR     — onde colocar o binário (padrão: ~/.local/bin)
#     PAPERLESS_VERSION         — versão específica (padrão: latest)
#     PAPERLESS_SKIP_OLLAMA     — "1" para não instalar o Ollama
#     PAPERLESS_LOCAL_ARCHIVE   — caminho para um .tar.gz local (pula download)
#                                 usado pelo CI de instalação

set -euo pipefail

# ─────────────────────────── Config ───────────────────────────

REPO="${PAPERLESS_REPO:-SEU_USUARIO/paperless}"
INSTALL_DIR="${PAPERLESS_INSTALL_DIR:-$HOME/.local/bin}"
VERSION="${PAPERLESS_VERSION:-latest}"
SKIP_OLLAMA="${PAPERLESS_SKIP_OLLAMA:-0}"
LOCAL_ARCHIVE="${PAPERLESS_LOCAL_ARCHIVE:-}"

# ─────────────────────────── Cores ────────────────────────────

if [ -t 1 ]; then
    BLUE='\033[38;5;75m'
    GREEN='\033[38;5;42m'
    YELLOW='\033[38;5;220m'
    RED='\033[38;5;197m'
    GREY='\033[38;5;244m'
    BOLD='\033[1m'
    RESET='\033[0m'
else
    BLUE='' GREEN='' YELLOW='' RED='' GREY='' BOLD='' RESET=''
fi

log()  { printf "${BLUE}==>${RESET} %s\n" "$*"; }
ok()   { printf "${GREEN}✓${RESET}  %s\n" "$*"; }
warn() { printf "${YELLOW}⚠${RESET}  %s\n" "$*"; }
err()  { printf "${RED}✗${RESET}  %s\n" "$*" >&2; exit 1; }

# ─────────────────────── Banner ────────────────────────

printf "\n"
printf "${BLUE}${BOLD}"
cat <<'EOF'
   ___                   _
  / _ \___ ____  ___ ____/ /__ ___ ___
 / ___/ _ `/ _ \/ -_) __/ / -_|_-<(_-<
/_/   \_,_/ .__/\__/_/ /_/\__/___/___/
         /_/
EOF
printf "${RESET}"
printf "${GREY}Seu assistente pessoal · 100%% offline${RESET}\n\n"

# ────────────────── Detecção de plataforma ──────────────────

OS="$(uname -s | tr '[:upper:]' '[:lower:]')"
ARCH="$(uname -m)"

case "$OS" in
    linux)  PLATFORM="linux" ;;
    darwin) PLATFORM="osx"   ;;
    *)      err "SO não suportado: $OS" ;;
esac

case "$ARCH" in
    x86_64|amd64)  RID_ARCH="x64"   ;;
    aarch64|arm64) RID_ARCH="arm64" ;;
    *)             err "Arquitetura não suportada: $ARCH" ;;
esac

RID="${PLATFORM}-${RID_ARCH}"
log "Plataforma detectada: ${BOLD}${RID}${RESET}"

# ───────────────────────── Preparação ───────────────────────

ASSET="paperless-${RID}.tar.gz"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

# ───────────────── Modo: arquivo local OU download ──────────

if [ -n "$LOCAL_ARCHIVE" ]; then
    [ -f "$LOCAL_ARCHIVE" ] || err "PAPERLESS_LOCAL_ARCHIVE aponta para arquivo inexistente: $LOCAL_ARCHIVE"
    log "Usando archive local: ${BOLD}${LOCAL_ARCHIVE}${RESET}"
    cp "$LOCAL_ARCHIVE" "$TMP/$ASSET"
else
    # ───────── Descobrir versão
    if [ "$VERSION" = "latest" ]; then
        log "Buscando versão mais recente..."
        VERSION="$(curl -fsSL "https://api.github.com/repos/${REPO}/releases/latest" \
            | grep '"tag_name"' \
            | head -n1 \
            | sed -E 's/.*"([^"]+)".*/\1/')"
        [ -z "$VERSION" ] && err "Não foi possível descobrir a última release"
    fi
    log "Versão: ${BOLD}${VERSION}${RESET}"

    URL="https://github.com/${REPO}/releases/download/${VERSION}/${ASSET}"
    log "Baixando ${ASSET}..."
    if ! curl -fL --progress-bar "$URL" -o "$TMP/$ASSET"; then
        err "Falha no download. URL tentada: $URL"
    fi
fi

# ───────────────────────── Extração ─────────────────────────

log "Extraindo..."
tar -xzf "$TMP/$ASSET" -C "$TMP"

# Sanidade: o archive precisa ter o binário
if [ ! -f "$TMP/paperless" ]; then
    err "Archive inválido: binário 'paperless' não encontrado após extração"
fi

# ───────────────────────── Instalação ───────────────────────

mkdir -p "$INSTALL_DIR"
mv "$TMP/paperless" "$INSTALL_DIR/paperless"
chmod +x "$INSTALL_DIR/paperless"

# appsettings.json e skill.md vão junto, mas NÃO sobrescrevem se já
# existirem — assim o usuário não perde customizações ao atualizar.
for f in appsettings.json skill.md; do
    if [ -f "$TMP/$f" ]; then
        if [ -f "$INSTALL_DIR/$f" ]; then
            warn "Mantendo $f existente (nova versão em $INSTALL_DIR/$f.new)"
            cp "$TMP/$f" "$INSTALL_DIR/$f.new"
        else
            cp "$TMP/$f" "$INSTALL_DIR/$f"
        fi
    fi
done

ok "Paperless instalado em ${BOLD}${INSTALL_DIR}/paperless${RESET}"

# ────────────────────── Aviso de PATH ───────────────────────

case ":$PATH:" in
    *:"$INSTALL_DIR":*)
        : # já está no PATH
        ;;
    *)
        warn "${BOLD}${INSTALL_DIR}${RESET} não está no seu PATH."
        printf "    Adicione esta linha ao seu ${BOLD}~/.bashrc${RESET} ou ${BOLD}~/.zshrc${RESET}:\n\n"
        printf "        ${BOLD}export PATH=\"%s:\$PATH\"${RESET}\n\n" "$INSTALL_DIR"
        ;;
esac

# ─────────────────── Instalação do Ollama ───────────────────

if [ "$SKIP_OLLAMA" = "1" ]; then
    warn "PAPERLESS_SKIP_OLLAMA=1 — pulando instalação do Ollama"
elif command -v ollama >/dev/null 2>&1; then
    ok "Ollama já instalado: $(ollama --version 2>&1 | head -n1)"
else
    warn "Ollama não encontrado."

    # Em CI/pipe sem TTY, não dá pra perguntar. Pulamos e avisamos.
    if [ ! -t 0 ] && [ ! -r /dev/tty ]; then
        warn "Stdin não é TTY — pulando prompt do Ollama."
        warn "Instale depois com: curl -fsSL https://ollama.com/install.sh | sh"
    else
        printf "    Deseja instalar agora? [${BOLD}Y${RESET}/n] "
        # Lê de /dev/tty para funcionar mesmo dentro de `| bash`
        read -r answer </dev/tty || answer=""

        if [[ ! "$answer" =~ ^[Nn]$ ]]; then
            log "Executando instalador oficial do Ollama..."
            curl -fsSL https://ollama.com/install.sh | sh
            ok "Ollama instalado"
        else
            warn "Pulando. Instale depois com:"
            printf "        ${BOLD}curl -fsSL https://ollama.com/install.sh | sh${RESET}\n"
        fi
    fi
fi

# ──────────────────────── Final ─────────────────────────────

printf "\n"
ok "Instalação concluída."
printf "    Execute: ${BOLD}paperless${RESET}\n\n"
printf "${GREY}Na primeira execução, o Paperless irá baixar os modelos automaticamente.${RESET}\n"