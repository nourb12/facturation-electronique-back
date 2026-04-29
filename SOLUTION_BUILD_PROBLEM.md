# Solution au Problème de Build - Einvoicing

## 🔴 Problème Identifié

Les fichiers DLL étaient verrouillés par deux processus :
1. **Microsoft Visual Studio 2022** (PID: 7124)
2. **Einvoicing.Api** (PID: 30652) - L'application en cours d'exécution

### Erreurs rencontrées :
```
error MSB3027: Impossible de copier "Einvoicing.Domain.dll" vers "bin\Debug\net8.0\Einvoicing.Domain.dll"
error MSB3027: Impossible de copier "Einvoicing.Application.dll" vers "bin\Debug\net8.0\Einvoicing.Application.dll"
error MSB3027: Impossible de copier "Einvoicing.Infrastructure.dll" vers "bin\Debug\net8.0\Einvoicing.Infrastructure.dll"
```

## ✅ Solution Appliquée

### Étape 1 : Identifier le processus qui verrouille les fichiers
```powershell
Get-Process | Where-Object {$_.ProcessName -like "*Einvoicing*"} | Select-Object Id, ProcessName, Path
```

### Étape 2 : Arrêter le processus
```powershell
Stop-Process -Id 30652 -Force
```

### Étape 3 : Rebuild la solution
```bash
dotnet build einvoicing/Einvoicing.sln
```

## 🎯 Résultat

✅ **Build réussi !** Tous les projets ont été compilés avec succès :
- ✅ Einvoicing.Domain
- ✅ Einvoicing.Application
- ✅ Einvoicing.Infrastructure
- ✅ Einvoicing.Tests
- ✅ Einvoicing.Api

## 🔧 Solutions Préventives

### Option 1 : Toujours arrêter l'application avant de rebuild
```powershell
# Arrêter tous les processus Einvoicing
Get-Process | Where-Object {$_.ProcessName -like "*Einvoicing*"} | Stop-Process -Force

# Puis rebuild
dotnet build
```

### Option 2 : Utiliser dotnet watch (Hot Reload)
```bash
cd Einvoicing.Api
dotnet watch run
```
Cela permet de recompiler automatiquement sans arrêter l'application.

### Option 3 : Fermer Visual Studio avant de compiler en ligne de commande
Si Visual Studio a l'application en mode debug, fermez-le ou arrêtez le debugging.

### Option 4 : Script PowerShell automatique
Créez un fichier `rebuild.ps1` :
```powershell
# Arrêter tous les processus Einvoicing
Get-Process | Where-Object {$_.ProcessName -like "*Einvoicing*"} | Stop-Process -Force -ErrorAction SilentlyContinue

# Attendre un peu
Start-Sleep -Seconds 2

# Rebuild
dotnet build Einvoicing.sln

# Optionnel : Relancer l'application
# cd Einvoicing.Api
# dotnet run
```

## 📝 Notes Importantes

1. **Toujours arrêter l'application** avant de faire un `dotnet build` manuel
2. Si vous utilisez Visual Studio, utilisez le bouton "Stop" avant de compiler
3. Les fichiers DLL ne peuvent pas être remplacés pendant qu'ils sont utilisés par un processus
4. Cette erreur est normale et courante dans le développement .NET

## 🚀 Pour Lancer l'Application

```bash
cd einvoicing/Einvoicing.Api
dotnet run
```

Ou avec hot reload :
```bash
cd einvoicing/Einvoicing.Api
dotnet watch run
```

---
**Date de résolution :** 28 avril 2026
**Statut :** ✅ RÉSOLU
