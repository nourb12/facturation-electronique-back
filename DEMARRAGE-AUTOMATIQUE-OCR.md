# 🤖 Démarrage automatique de l'OCR - Comment ça marche ?

## ✅ OUI, l'OCR démarre automatiquement !

Vous n'avez **RIEN à faire manuellement**. Tout est automatique.

## 🔧 Configuration actuelle

### 1. AutoStart activé ✅

**Fichier :** `Einvoicing.Api/appsettings.Development.json`
```json
{
    "OcrService": {
        "AutoStart": true    ← ACTIVÉ !
    }
}
```

### 2. Code de démarrage automatique ✅

**Fichier :** `Einvoicing.Api/Program.cs` (ligne 199)
```csharp
// Dev only: auto-start local OCR microservice if missing.
await OcrServiceDevLauncher.EnsureRunningAsync(
    app.Configuration, 
    app.Environment, 
    app.Logger, 
    app.Lifetime.ApplicationStopping
);
```

### 3. Dépendances Python installées ✅

**Dossier :** `ocr_service/.venv/`
- PaddleOCR ✅
- FastAPI ✅
- Uvicorn ✅
- Toutes les dépendances ✅

## 🚀 Workflow automatique

### Quand vous lancez le backend :

```
┌─────────────────────────────────────────────────────────────┐
│  Vous : cd Einvoicing.Api && dotnet run                     │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│  Backend .NET démarre                                        │
│  [INFO] Starting application...                             │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│  OcrServiceDevLauncher.EnsureRunningAsync() s'exécute       │
│  Vérifie : http://localhost:8000/health répond ?            │
└─────────────────────────────────────────────────────────────┘
                            ↓
                    ┌───────┴───────┐
                    │               │
                ✅ OUI          ❌ NON
                    │               │
                    ↓               ↓
        ┌───────────────┐   ┌──────────────────────────┐
        │ OCR déjà      │   │ Démarrage automatique :  │
        │ démarré       │   │                          │
        │ → Continue    │   │ 1. Trouve ocr_service/   │
        └───────────────┘   │ 2. Trouve .venv/         │
                            │ 3. Lance Python :        │
                            │    python -m uvicorn ... │
                            │ 4. Attend /health (10s)  │
                            └──────────────────────────┘
                                        ↓
┌─────────────────────────────────────────────────────────────┐
│  [INFO] Démarrage OCR service: python.exe -m uvicorn ...    │
│  [INFO] OCR service prêt: http://localhost:8000/health      │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│  Backend continue normalement                                │
│  [INFO] Application started. Press Ctrl+C to shut down.     │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│  L'OCR est maintenant actif en arrière-plan                 │
│  http://localhost:8000 ← Service OCR                        │
│  http://localhost:5051 ← Backend .NET                       │
└─────────────────────────────────────────────────────────────┘
```

### Quand vous arrêtez le backend :

```
┌─────────────────────────────────────────────────────────────┐
│  Vous : Ctrl+C (arrêt du backend)                           │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│  Backend s'arrête                                            │
│  [INFO] Application is shutting down...                     │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│  L'OCR s'arrête AUTOMATIQUEMENT aussi                       │
│  Le processus Python est tué automatiquement                │
└─────────────────────────────────────────────────────────────┘
```

## 🎯 Ce que vous devez faire

### Pour démarrer tout le système :

```powershell
# 1. Démarrer le backend (qui démarre l'OCR automatiquement)
cd c:\backendpfe\einvoicing\Einvoicing.Api
dotnet run

# C'est tout ! L'OCR démarre automatiquement.
```

### Pour arrêter tout le système :

```powershell
# Appuyez sur Ctrl+C dans le terminal du backend
# L'OCR s'arrête automatiquement aussi
```

## 📊 Logs à surveiller

Quand le backend démarre, vous verrez ces logs :

```
[INFO] Starting application...
[INFO] Démarrage OCR service: C:\...\python.exe -m uvicorn main:app --host 127.0.0.1 --port 8000
[INFO] OCR service prêt: http://localhost:8000/health
[INFO] Application started. Press Ctrl+C to shut down.
[INFO] Now listening on: http://localhost:5051
```

Si vous voyez `[INFO] OCR service prêt`, c'est que **tout fonctionne** ! ✅

## 🔍 Vérifications

### 1. Vérifier que l'OCR est démarré

Pendant que le backend tourne, ouvrez un autre terminal :

```powershell
# Vérifier le service OCR
curl http://localhost:8000/health
```

**Résultat attendu :**
```json
{"status":"ok","engine":"PaddleOCR","languages":["latin","arabic"]}
```

### 2. Vérifier les processus

```powershell
# Voir les processus Python (OCR)
Get-Process python

# Voir les processus .NET (Backend)
Get-Process dotnet
```

Vous devriez voir :
- Un processus `python.exe` (le service OCR)
- Un processus `dotnet.exe` (le backend .NET)

## 🚨 Que faire si l'OCR ne démarre pas automatiquement ?

### Vérification 1 : AutoStart activé ?

```powershell
cd c:\backendpfe\einvoicing\Einvoicing.Api
cat appsettings.Development.json | Select-String "AutoStart"
```

Devrait afficher : `"AutoStart": true`

### Vérification 2 : Dépendances installées ?

```powershell
cd c:\backendpfe\einvoicing\ocr_service
Test-Path .venv
```

Devrait afficher : `True`

### Vérification 3 : Python accessible ?

```powershell
cd c:\backendpfe\einvoicing\ocr_service
.\.venv\Scripts\python.exe --version
```

Devrait afficher : `Python 3.11.9`

### Solution : Réinstaller les dépendances

```powershell
cd c:\backendpfe\einvoicing\ocr_service
.\INSTALL_OCR.bat
```

## 💡 Démarrage manuel (si besoin)

Si pour une raison quelconque vous voulez démarrer l'OCR manuellement :

**Terminal 1 - OCR :**
```powershell
cd c:\backendpfe\einvoicing\ocr_service
.\START_OCR.bat
```

**Terminal 2 - Backend :**
```powershell
cd c:\backendpfe\einvoicing\Einvoicing.Api
dotnet run
```

Mais normalement, **vous n'avez pas besoin de faire ça** !

## 🎓 Comprendre le code

### OcrServiceDevLauncher.cs

Ce fichier contient toute la logique de démarrage automatique :

```csharp
public static async Task EnsureRunningAsync(...)
{
    // 1. Vérifie si on est en mode Development
    if (!env.IsDevelopment())
        return;

    // 2. Vérifie si AutoStart est activé
    var autoStart = config.GetValue("OcrService:AutoStart", true);
    if (!autoStart)
        return;

    // 3. Vérifie si l'OCR répond déjà
    if (await IsHealthyAsync(healthUrl))
        return;  // Déjà démarré, rien à faire

    // 4. Trouve le dossier ocr_service
    var workDir = ResolveWorkDir(config, env.ContentRootPath);

    // 5. Trouve l'exécutable Python
    var pythonExe = ResolvePythonExe(config, workDir);

    // 6. Démarre le processus Python
    var proc = Process.Start(psi);

    // 7. Enregistre l'arrêt automatique
    appStopping.Register(() => {
        if (!proc.HasExited)
            proc.Kill(entireProcessTree: true);
    });

    // 8. Attend que /health réponde
    for (var i = 0; i < 20; i++) {
        if (await IsHealthyAsync(healthUrl))
            return;  // Prêt !
        await Task.Delay(500);
    }
}
```

## ✅ Résumé

| Question | Réponse |
|----------|---------|
| L'OCR démarre automatiquement ? | ✅ **OUI** |
| Je dois lancer un script ? | ❌ **NON** |
| Je dois ouvrir 2 terminaux ? | ❌ **NON** |
| Je dois faire quelque chose de spécial ? | ❌ **NON** |
| Je lance juste `dotnet run` ? | ✅ **OUI** |
| L'OCR s'arrête automatiquement aussi ? | ✅ **OUI** |

## 🎉 Conclusion

**Vous n'avez RIEN à faire !**

Démarrez simplement le backend :
```powershell
cd c:\backendpfe\einvoicing\Einvoicing.Api
dotnet run
```

Et l'OCR démarre automatiquement en arrière-plan ! 🚀

C'est aussi simple que ça. Pas de script à lancer, pas de terminal supplémentaire, pas de configuration manuelle. **Tout est automatique.**

---

**Prochaine étape :** Démarrez le backend et vérifiez les logs pour voir `[INFO] OCR service prêt` ! 🎯
