# LanUsb — Darmowa alternatywa dla VirtualHere (USB przez LAN)

W 100% darmowa aplikacja Open Source (licencja MIT) do udostępniania urządzeń USB przez sieć lokalną (LAN) w układzie **Serwer + Klient**, stworzona z myślą o streamowaniu (np. Stream Deck, kamera 4K, mikrofon, interfejs audio, słuchawki na drugim PC).

Brak jakichkolwiek licencji, limitów urządzeń czy płatnych subskrypcji.

---

## ⚠️ BARDZO WAŻNY WYMÓG: Sieć prywatna w Windows!

> [!IMPORTANT]
> **Profil sieci w systemie Windows (Wi-Fi lub Ethernet) MUSI być ustawiony jako "Prywatna" (Private), a NIE "Publiczna".**
> - W sieci **Publicznej** zapora systemu Windows ze względów bezpieczeństwa całkowicie blokuje ruch LAN, wykrywanie innych komputerów (UDP `32400`) oraz przesyłanie USB (TCP `3240`).
> - **Gdy włączysz sieć Prywatną, aplikacja natychmiast zadziała!**
> - Bezpośrednio w programie (w Serwerze i Kliencie) znajduje się pasek z przyciskiem **🌐 Ustawienia sieci** (otwiera ustawienia Windows) oraz **⚡ Zmień na Prywatną** (automatyczne przestawienie profilu).

---

## ⬇️ Automatyczne pobieranie sterowników w programie

W obu aplikacjach znajduje się bezpośredni przycisk **⬇️ Pobierz sterownik**:
- **LanUsb.Server**: automatycznie sprawdza, pobiera z oficjalnego repozytorium GitHub najnowszy instalator `usbipd-win` i uruchamia instalację.
- **LanUsb.Client**: automatycznie pobiera najnowszy podpisany sterownik `usbip-win2` bezpośrednio z GitHub i instaluje wirtualny kontroler USB w systemie.

---

## Szybki start

### 1. Komputer z urządzeniami USB (Serwer)
1. Uruchom `LanUsb.Server.exe` (najlepiej **jako Administrator**).
2. Jeśli sterownik nie jest jeszcze zainstalowany, kliknij przycisk **⬇️ Pobierz sterownik** (program pobierze i zainstaluje `usbipd-win`).
3. Upewnij się, że Twoja sieć w Windows to **Prywatna** (użyj przycisku w programie, jeśli trzeba).
4. Zaznacz urządzenie na liście i kliknij **🟢 Udostępnij (Bind)** (lub podwójny klik na wierszu).

### 2. Komputer do streamowania (Klient)
1. Skopiuj na komputer do streamowania folder `LanUsb.Client\bin\Release\net8.0-windows\` (oraz opcjonalnie `Otworz-Porty-Firewall.bat`).
2. Uruchom `LanUsb.Client.exe`.
3. Jeśli sterownik nie jest jeszcze zainstalowany, kliknij **⬇️ Pobierz sterownik** (program sam pobierze i zainstaluje `usbip-win2`).
4. Upewnij się, że sieć to **Prywatna**.
5. Serwer w sieci LAN pojawi się automatycznie na liście w ciągu ~2 sekund (lub dodaj jego IP ręcznie w polu na górze).
6. Kliknij dwukrotnie wybrane urządzenie lub wybierz je i kliknij **🚀 Podłącz (Attach)** — pojawi się w Windows tak, jakby było fizycznie wpięte do portu USB!
7. Kliknij **⭐ Auto-łącz**, aby urządzenie łączyło się samoczynnie przy każdym włączeniu komputera lub restarcie serwera.

---

## Porty sieciowe (Firewall Windows)

Uruchom plik `Otworz-Porty-Firewall.bat` (automatycznie doda reguły w Zaporze Windows) lub zezwól na:
- **TCP `3240`** — ruch USB/IP (`usbipd`)
- **UDP `32400`** — automatyczne wykrywanie serwerów w LAN (`LanUsb`)
- **TCP `32401`** — zdalne Bind/Unbind (`LanUsb`)

---

## ☕ Wesprzyj Autora (Dotacja)

Aplikacja LanUsb jest i zawsze będzie w 100% darmowa. Jeśli program ułatwił Ci streamowanie lub zaoszczędził wydatek na komercyjne licencje, możesz dobrowolnie wesprzeć autora:
- **BuyCoffee:** [https://buycoffee.to/pelek2g](https://buycoffee.to/pelek2g) (BLIK, szybki przelew, karta, Apple Pay, Google Pay)
- **PayPal:** `szefkacperek@gmail.com`

---

## Kompilacja ze źródeł (.NET 8)

```powershell
dotnet build LanUsb.sln -c Release
```

Pliki wykonywalne:
- `LanUsb.Server\bin\Release\net8.0-windows\LanUsb.Server.exe`
- `LanUsb.Client\bin\Release\net8.0-windows\LanUsb.Client.exe`
