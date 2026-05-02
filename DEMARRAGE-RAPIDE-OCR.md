# 🚀 Démarrage rapide - OCR configuré !

## ✅ Installation terminée

Les dépendances Python sont maintenant installées. L'OCR est prêt à fonctionner !

## 🎯 Comment utiliser l'OCR

### Option 1 : Démarrage automatique (RECOMMANDÉ)

**Il n'y a rien à faire !** 

Quand vous démarrez le backend .NET, l'OCR démarre automatiquement :

```powershell
cd c:\backendpfe\einvoicing\Einvoicing.Api
dotnet run
```

Le backend va :
1. Détecter que l'OCR n'est pas démarré
2. Le démarrer automatiquement en arrière-plan
3. Attendre qu'il soit prêt
4. Afficher dans les logs : `[INFO] OCR service prêt: http://localhost:8000/health`

### Option 2 : Démarrage manuel (pour tester)

Si vous voulez tester l'OCR indépendamment :

```powershell
cd c:\backendpfe\einvoicing\ocr_service
.\START_OCR.bat
```

Puis dans un autre terminal :
```powershell
cd c:\backendpfe\einvoicing\Einvoicing.Api
dotnet run
```

## 🧪 Tester que l'OCR fonctionne

### Test 1 : Vérifier le service OCR
Ouvrez votre navigateur : http://localhost:8000/health

Vous devriez voir :
```json
{"status": "healthy", "service": "OCR TunisFlow"}
```

### Test 2 : Vérifier dans l'application
1. Démarrez le backend .NET
2. Démarrez le frontend Angular
3. Connectez-vous en tant qu'admin
4. Allez sur "Demandes KYC"
5. Cliquez sur une demande avec des documents

**Avant (OCR non configuré) :**
```
❌ Service OCR non configuré - validation manuelle requise
```

**Maintenant (OCR actif) :**
```
✅ OCR exploitable (87% de confiance)
✅ Champs remplis automatiquement
✅ Comparaisons formulaire/OCR disponibles
```

## 📊 Ce que l'OCR fait automatiquement

Quand une demande KYC est soumise avec des documents :

1. **Extraction automatique** des données depuis :
   - Registre de commerce
   - Patente
   - CIN du responsable
   - RIB bancaire

2. **Détection automatique** de :
   - Matricule fiscal
   - Raison sociale
   - Nom du gérant
   - Forme juridique
   - Adresse
   - Date de création

3. **Comparaison automatique** :
   - Formulaire vs Documents
   - Détection des incohérences
   - Score de confiance

4. **Scoring KYC amélioré** :
   - Utilise les données OCR
   - Détecte les anomalies
   - Suggère acceptation/refus

## 🎛️ Logs à surveiller

### Backend .NET
```
[INFO] Démarrage OCR service: C:\...\python.exe -m uvicorn main:app --host 127.0.0.1 --port 8000
[INFO] OCR service prêt: http://localhost:8000/health
```

### Service OCR
```
INFO:     Started server process
INFO:     Waiting for application startup.
INFO:     Application startup complete.
INFO:     Uvicorn running on http://127.0.0.1:8000
```

## 🔧 Commandes utiles

### Vérifier si l'OCR est démarré
```powershell
curl http://localhost:8000/health
```

### Voir les processus Python
```powershell
Get-Process python
```

### Arrêter l'OCR manuellement
```powershell
Stop-Process -Name python -Force
```

### Redémarrer l'OCR
Arrêtez le backend .NET, puis redémarrez-le. L'OCR redémarrera automatiquement.

## 📝 Prochaines étapes

1. ✅ **Démarrez le backend** : `cd Einvoicing.Api && dotnet run`
2. ✅ **Vérifiez les logs** : Cherchez "OCR service prêt"
3. ✅ **Testez l'application** : Allez sur "Demandes KYC"
4. ✅ **Vérifiez les données** : Les champs doivent se remplir automatiquement

## 🎉 Résultat

Maintenant, **à chaque démarrage du backend** :
- ✅ L'OCR démarre automatiquement
- ✅ Les documents sont analysés automatiquement
- ✅ Les champs se remplissent automatiquement
- ✅ Le scoring KYC est plus précis
- ✅ Vous gagnez un temps précieux !

## 🚨 En cas de problème

Si l'OCR ne démarre pas automatiquement :

1. Vérifiez les logs du backend
2. Vérifiez que Python est accessible : `python --version`
3. Vérifiez que les dépendances sont installées : `cd ocr_service && .\.venv\Scripts\activate && python -c "import paddleocr"`
4. Démarrez l'OCR manuellement : `cd ocr_service && .\START_OCR.bat`
5. Consultez le guide complet : `CONFIGURATION-OCR-AUTO.md`

## 📞 Support

Si vous rencontrez des problèmes, consultez :
- `CONFIGURATION-OCR-AUTO.md` - Guide complet
- `GUIDE-OCR-FIX.md` - Guide de dépannage
- Logs du backend .NET
- Logs du service OCR
