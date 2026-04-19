#!/usr/bin/env bash
#
# Paperless installer — Linux & macOS
#
# Uso:
#     curl -fsSL https://github.com/SEU_USUARIO/paperless/releases/latest/download/install.sh | bash
#
# Variáveis opcionais:
#     PAPERLESS_INSTALL_DIR   — onde colocar o binário (padrão: ~/.local/bin)
#     PAPERLESS_VERSION       — versão específica (padrão: latest)
#     PAPERLESS_SKIP_OLLAMA   — "1" para não instalar o Ollama

set -euo pipefail

# ─────────────────────────── Config ───────────────────────────

REPO="${PAPERLESS_REPO:-SEU_USUARIO/paperless}"
INSTALL_DIR="${PAPERLESS_INSTALL_DIR:-$HOME/.local/bin}"
VERSION="${PAPERLESS_VERSION:-latest}"
SKIP_OLLAMA="${PAPERLESS_SKIP_OLLAMA:-0}"

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

# ──────────────────── Descobrir versão ──────────────────────

if [ "$VERSION" = "latest" ]; then
    log "Buscando versão mais recente..."
    VERSION="$(curl -fsSL "https://api.github.com/repos/${REPO}/releases/latest" \
        | grep '"tag_name"' \
        | head -n1 \
        | sed -E 's/.*"([^"]+)".*/\1/')"

    [ -z "$VERSION" ] && err "Não foi possível descobrir a última release"
fi
log "Versão: ${BOLD}${VERSION}${RESET}"

# ───────────────────────── Download ─────────────────────────

ASSET="paperless-${RID}.tar.gz"
URL="https://github.com/${REPO}/releases/download/${VERSION}/${ASSET}"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

log "Baixando ${ASSET}..."
if ! curl -fL --progress-bar "$URL" -o "$TMP/$ASSET"; then
    err "Falha no download. URL tentada: $URL"
fi

log "Extraindo..."
tar -xzf "$TMP/$ASSET" -C "$TMP"

# ───────────────────────── Instalação ───────────────────────

mkdir -p "$INSTALL_DIR"
mv "$TMP/paperless" "$INSTALL_DIR/paperless"
chmod +x "$INSTALL_DIR/paperless"

# appsettings.json vai para ~/.config/paperless/ se não existir lá,
# mas o binário lê de AppContext.BaseDirectory — que é onde o binário
# está.
if [ -f "$TMP/appsettings.json" ]; then
    cp "$TMP/appsettings.json" "$INSTALL_DIR/appsettings.json"
fi
if [ -f "$TMP/skill.md" ]; then
    cp "$TMP/skill.md" "$INSTALL_DIR/skill.md"
fi

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
    printf "    Deseja instalar agora? [${BOLD}Y${RESET}/n] "
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

# ──────────────────────── Final ─────────────────────────────

printf "\n"
ok "Instalação concluída."
printf "    Execute: ${BOLD}paperless${RESET}\n\n"
printf "${GREY}Na primeira execução, o Paperless irá baixar os modelos automaticamente.${RESET}\n"