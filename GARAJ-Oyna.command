#!/bin/bash
# GARAJ — macOS'ta çift tıkla başlat.
# Finder'da bu dosyaya çift tıklayınca Terminal açılır ve oyun başlar.

cd "$(dirname "$0")"

# dotnet genelde PATH'te olmaz (Homebrew kurulumu). Olası yerleri ekle.
export PATH="/opt/homebrew/bin:/usr/local/bin:/usr/local/share/dotnet:$HOME/.dotnet:$PATH"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "HATA: dotnet bulunamadı."
  echo "Kurmak için terminalde:  brew install dotnet"
  echo
  read -p "Kapatmak için Enter'a bas..."
  exit 1
fi

clear
echo "GARAJ derleniyor ve başlatılıyor (ilk sefer biraz sürebilir)..."
echo
dotnet run --project src/Garaj.Console -c Release -- "$@"

echo
read -p "Oyun kapandı. Pencereyi kapatmak için Enter'a bas..."
