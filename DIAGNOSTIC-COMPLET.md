# 🔍 Diagnostic Complet - Timeout Scan Mobile

## Date: 10 mai 2026

## ✅ Vérifications Effectuées

### 1. Service OCR (Port 8000)
- **Status**: ✅ **TOURNE**
- **Localhost**: `127.0.0.1:8000` → ✅ Accessible
- **Réseau**: Non testé (pas nécessaire, backend y accède en localhost)

### 2. Backend API (Port 5051)
- **Status**: ⚠️ **PROBLÈME TROUVÉ**
- **Localhost**: `127.0.0.1:5051` → ✅ Accessible
- **Réseau**: `192.168.1.123:5051` → ❌ **INACCESSIBLE**

## 🚨 Cause Racine du Timeout

Le backend tourne avec le profil **`https`** qui écoute sur `localhost:5051` uniquement.

Le mobile essaie de se connecter via `192.168.1.123:5051` (réseau Wi-Fi) mais le backend n'écoute pas sur cette interface réseau.

## 📋 Configuration Backend

**Fichier**: `Einvoicing.Api/Properties/launchSettings.json`

### Profils disponibles:

1. **`http`** ✅ (À UTILISER)
   ```json
   "applicationUrl": "http://0.0.0.0:5051"
   ```
   → Écoute sur **toutes les interfaces réseau** (localhost + Wi-Fi)

2. **`https`** ❌ (ACTUELLEMENT UTILISÉ)
   ```json
   "applicationUrl": "http://localhost:5051"
   ```
   → Écoute sur **localhost uniquement**

3. **`IIS Express`**
   → IIS Express (pas recommandé pour dev mobile)

## ✅ Solution

### Option 1: Utiliser le script BAT (RECOMMANDÉ)

Double-cliquez sur:
```
einvoicing/DEMARRER-BACKEND-RESEAU.bat
```

### Option 2: Ligne de commande

```bash
cd c:\backendpfe\einvoicing\Einvoicing.Api
dotnet run --launch-profile http
```

### Option 3: Visual Studio

1. Ouvrir Visual Studio
2. En haut, à côté du bouton ▶️ Play
3. Sélectionner **`http`** au lieu de `https` ou `IIS Express`
4. Cliquer sur ▶️ Play

## 🧪 Vérification

Après avoir redémarré le backend avec le profil `http`:

### Test 1: Depuis le PC
```bash
curl http://192.168.1.123:5051/health
```

### Test 2: Depuis le mobile
1. Ouvrir l'app mobile
2. Aller sur l'onglet Scan
3. Importer une image depuis la galerie
4. ✅ Devrait fonctionner maintenant!

## 📊 Résumé des Timeouts

| Composant | Timeout | Status |
|-----------|---------|--------|
| Mobile → Backend | 120s | ✅ Augmenté |
| Backend → OCR | 360s | ✅ OK |
| OCR Processing | Variable | ✅ Tourne |

## 🎯 Checklist Finale

- [x] Service OCR tourne sur port 8000
- [x] Backend tourne sur localhost:5051
- [ ] **Backend doit tourner sur 0.0.0.0:5051** ← **ACTION REQUISE**
- [x] Mobile configuré avec IP `192.168.1.123`
- [x] Timeout mobile augmenté à 120s

## 📝 Notes

- Le service OCR n'a pas besoin d'être accessible depuis le réseau
- Seul le backend doit être accessible depuis le mobile
- L'OCR communique avec le backend en localhost (plus rapide)

## 🔄 Prochaines Étapes

1. **Arrêter le backend actuel** (Ctrl+C dans le terminal)
2. **Redémarrer avec le profil `http`** (voir Solution ci-dessus)
3. **Tester depuis le mobile**
4. **Si ça marche**: Tout est réglé! 🎉
5. **Si ça ne marche pas**: Vérifier le pare-feu Windows

## 🛡️ Pare-feu Windows (si nécessaire)

Si après avoir redémarré avec le profil `http`, le mobile ne peut toujours pas se connecter:

```powershell
# Autoriser le port 5051 dans le pare-feu
netsh advfirewall firewall add rule name="Backend TunisFlow" dir=in action=allow protocol=TCP localport=5051
```

## 📞 Support

Si le problème persiste après avoir suivi ces étapes, vérifier:
1. Le mobile et le PC sont sur le même réseau Wi-Fi
2. L'IP du PC est toujours `192.168.1.123` (peut changer)
3. Le pare-feu Windows n'est pas trop restrictif
