# ITDoku (.NET 8 MVC + EF Core + SQL Server)

## Start (Visual Studio / CLI)
1. Passen Sie `appsettings.json` an (ConnectionStrings:Default).
2. In der Paket-Manager-Konsole:
   ```powershell
   dotnet tool install --global dotnet-ef
   dotnet ef migrations add Initial
   dotnet ef database update
   ```
3. Zugangsdaten anpassen : appsettings.json
4. SecretKey ersetzen: appsettings.json
   32-Byte Key erzeugen (PowerShell):
   [Convert]::ToBase64String((1..32 | % {Get-Random -Max 256}))
3. Starten (F5).
 
Die Baumstruktur erlaubt max. 5 Ebenen (+Root). Dateien können hochgeladen, ersetzt (mit Versionierung) und gelöscht werden.
