<#
.SYNOPSIS
    Paperless installer — Windows

.DESCRIPTION
    Uso (PowerShell):
        iwr -useb https://github.com/SEU_USUARIO/paperless/releases/latest/download/install.ps1 | iex

    Variáveis opcionais (definidas antes de rodar):
        $env:PAPERLESS_INSTALL_DIR   — onde instalar (padrão: $env:LOCALAPPDATA\Paperless)
        $env:PAPERLESS_VERSION       — versão específica (padrão: latest)
        $env:PAPERLESS_SKIP_OLLAMA   — "1" para pular instalação do Ollama
#>

#Requires -Version 5.1
$ErrorActionPreference = 'Stop'

# ─────────────────────────── Config ───────────────────────────

$Repo        = if ($env:PAPERLESS_REPO) { $env:PAPERLESS_REPO } else { 'SEU_USUARIO/paperless' }
$InstallDir  = if ($env:PAPERLESS_INSTALL_DIR) { $env:PAPERLESS_INSTALL_DIR } else { "$env:LOCALAPPDATA\Paperless" }
$Version     = if ($env:PAPERLESS_VERSION) { $env:PAPERLESS_VERSION } else { 'latest' }
$SkipOllama  = $env:PAPERLESS_SKIP_OLLAMA -eq '1'

# ─────────────────────────── Helpers ──────────────────────────

function Write-Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "✓  $msg" -ForegroundColor Green }
function Write-Warn2($msg){ Write-Host "⚠  $msg" -ForegroundColor Yellow }
function Write-Err($msg)  { Write-Host "✗  $msg" -ForegroundColor Red; exit 1 }

# ─────────────────────────── Banner ───────────────────────────

Write-Host ''
Write-Host '   ___                   _' -ForegroundColor Cyan
Write-Host '  / _ \___ ____  ___ ____/ /__ ___ ___' -ForegroundColor Cyan
Write-Host ' / ___/ _ `/ _ \/ -_) __/ / -_|_-<(_-<' -ForegroundColor Cyan
Write-Host '/_/   \_,_/ .__/\__/_/ /_/\__/___/___/' -ForegroundColor Cyan
Write-Host '         /_/' -ForegroundColor Cyan
Write-Host 'Seu assistente pessoal · 100% offline' -ForegroundColor DarkGray
Write-Host ''

# ──────────────────── Detecção de arquitetura ─────────────────

if (-not [Environment]::Is64BitOperatingSystem) {
    Write-Err "Apenas sistemas Windows 64-bit são suportados."
}

$arch = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' } else { 'x64' }
$rid  = "win-$arch"
Write-Step "Plataforma detectada: $rid"

# ──────────────────── Descobrir versão ────────────────────────

if ($Version -eq 'latest') {
    Write-Step 'Buscando versão mais recente...'
    try {
        $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases/latest" -UseBasicParsing
        $Version = $release.tag_name
    } catch {
        Write-Err "Não foi possível descobrir a última release: $_"
    }
}
Write-Step "Versão: $Version"

# ───────────────────────── Download ───────────────────────────

$assetName = "paperless-$rid.zip"
$url       = "https://github.com/$Repo/releases/download/$Version/$assetName"
$tmp       = Join-Path $env:TEMP "paperless-install-$(Get-Random)"
$null      = New-Item -ItemType Directory -Path $tmp -Force
$zipPath   = Join-Path $tmp $assetName

try {
    Write-Step "Baixando $assetName..."
    Invoke-WebRequest -Uri $url -OutFile $zipPath -UseBasicParsing

    # ───────────────────── Instalação ─────────────────────

    $null = New-Item -ItemType Directory -Force -Path $InstallDir
    Expand-Archive -Path $zipPath -DestinationPath $InstallDir -Force
    Write-Ok "Instalado em $InstallDir\paperless.exe"
}
finally {
    Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
}

# ─────────────────── PATH do usuário ──────────────────────────

$userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
if ($userPath -notlike "*$InstallDir*") {
    Write-Step 'Adicionando ao PATH do usuário...'
    [Environment]::SetEnvironmentVariable('Path', "$userPath;$InstallDir", 'User')
    Write-Warn2 'Reinicie o terminal para o PATH atualizar nesta sessão.'
}

# ─────────────────── Instalação do Ollama ─────────────────────

if ($SkipOllama) {
    Write-Warn2 'PAPERLESS_SKIP_OLLAMA=1 — pulando instalação do Ollama'
}
elseif (Get-Command ollama -ErrorAction SilentlyContinue) {
    Write-Ok 'Ollama já instalado'
}
else {
    Write-Warn2 'Ollama não encontrado.'
    $answer = Read-Host 'Deseja instalar agora? [Y/n]'
    if ($answer -notmatch '^[Nn]') {
        Write-Step 'Baixando instalador do Ollama...'
        $ollamaInstaller = Join-Path $env:TEMP 'OllamaSetup.exe'
        Invoke-WebRequest -Uri 'https://ollama.com/download/OllamaSetup.exe' `
                          -OutFile $ollamaInstaller -UseBasicParsing

        Write-Step 'Executando instalador (vai abrir UAC)...'
        Start-Process -FilePath $ollamaInstaller -Wait
        Write-Ok 'Instalador do Ollama finalizado'
    } else {
        Write-Warn2 'Pulando. Instale depois em: https://ollama.com'
    }
}

# ──────────────────────── Final ───────────────────────────────

Write-Host ''
Write-Ok 'Instalação concluída.'
Write-Host "    Execute: paperless" -ForegroundColor White
Write-Host ''
Write-Host 'Na primeira execução, o Paperless irá baixar os modelos automaticamente.' -ForegroundColor DarkGray