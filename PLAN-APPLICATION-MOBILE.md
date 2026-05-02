# 📱 Plan de Développement - Application Mobile

## 🎯 Objectif

Créer une application mobile (iOS + Android) qui permet de :
1. Se connecter avec son compte
2. Scanner une facture/BL/devis papier
3. Extraire automatiquement les données (OCR)
4. Enregistrer dans le dashboard web

## ✅ Faisabilité : EXCELLENTE

Vous avez déjà :
- ✅ Backend .NET avec API
- ✅ Service OCR fonctionnel
- ✅ Base de données
- ✅ Authentification

Il manque juste : L'application mobile

## 🏆 Technologie Recommandée : Flutter

**Pourquoi Flutter ?**
- ✅ Un seul code pour iOS + Android
- ✅ Performance native
- ✅ Excellentes bibliothèques caméra/OCR
- ✅ Développement rapide
- ✅ Gratuit et open-source

## 📋 Plan d'Action Complet

### PHASE 1 : Backend Mobile API (2 jours)

#### Jour 1 : Créer les endpoints mobile

**Fichier à créer :** `Einvoicing.Api/Controllers/MobileController.cs`

```csharp
[ApiController]
[Route("api/mobile")]
public class MobileController : ControllerBase
{
    // Authentification mobile
    [HttpPost("auth/login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // Vérifier email/password
        // Générer JWT token
        // Retourner token + user info
    }

    // Scanner une facture
    [HttpPost("factures/scan")]
    [Authorize]
    public async Task<IActionResult> ScanFacture([FromForm] IFormFile image)
    {
        // Envoyer l'image au service OCR
        // Extraire les données
        // Retourner les données structurées
    }

    // Liste des factures de l'utilisateur
    [HttpGet("factures")]
    [Authorize]
    public async Task<IActionResult> GetFactures()
    {
        // Récupérer les factures de l'utilisateur connecté
    }

    // Créer une facture depuis le mobile
    [HttpPost("factures")]
    [Authorize]
    public async Task<IActionResult> CreateFacture([FromBody] CreateFactureRequest request)
    {
        // Créer la facture dans la base
    }
}
```

#### Jour 2 : Configurer JWT pour mobile

**Fichier à modifier :** `Einvoicing.Api/Program.cs`

```csharp
// Ajouter JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });
```

**Fichier à modifier :** `appsettings.json`

```json
{
  "Jwt": {
    "Key": "VotreCléSecrèteTrèsLongueEtSécurisée123456789",
    "Issuer": "EInvoicing",
    "Audience": "EInvoicingMobile",
    "ExpirationMinutes": 10080
  }
}
```

### PHASE 2 : Setup Flutter (1 jour)

#### Étape 2.1 : Installer Flutter

**Windows :**
1. Télécharger : https://docs.flutter.dev/get-started/install/windows
2. Extraire dans `C:\flutter`
3. Ajouter au PATH : `C:\flutter\bin`
4. Vérifier : `flutter doctor`

#### Étape 2.2 : Créer le projet

```bash
# Créer le projet
flutter create einvoicing_mobile
cd einvoicing_mobile

# Tester
flutter run
```

#### Étape 2.3 : Structure du projet

```
einvoicing_mobile/
├── lib/
│   ├── main.dart
│   ├── models/
│   │   ├── user.dart
│   │   ├── invoice.dart
│   │   └── invoice_data.dart
│   ├── screens/
│   │   ├── login_screen.dart
│   │   ├── dashboard_screen.dart
│   │   ├── scan_screen.dart
│   │   └── invoice_detail_screen.dart
│   ├── services/
│   │   ├── api_service.dart
│   │   ├── auth_service.dart
│   │   └── storage_service.dart
│   ├── widgets/
│   │   ├── invoice_card.dart
│   │   └── custom_button.dart
│   └── utils/
│       ├── constants.dart
│       └── helpers.dart
├── pubspec.yaml
└── README.md
```

#### Étape 2.4 : Dépendances

**Fichier :** `pubspec.yaml`

```yaml
dependencies:
  flutter:
    sdk: flutter

  # API & Authentification
  http: ^1.1.0
  dio: ^5.4.0
  flutter_secure_storage: ^9.0.0
  jwt_decoder: ^2.0.1

  # Caméra & Images
  camera: ^0.10.5
  image_picker: ^1.0.7
  image: ^4.1.3

  # OCR (optionnel)
  google_mlkit_text_recognition: ^0.11.0

  # UI
  flutter_svg: ^2.0.9
  cached_network_image: ^3.3.1
  shimmer: ^3.0.0

  # State Management
  provider: ^6.1.1

  # Utilitaires
  intl: ^0.18.1
  path_provider: ^2.1.1
```

### PHASE 3 : Développement Mobile (7 jours)

#### Jour 1 : Configuration & Constants

**Fichier :** `lib/utils/constants.dart`

```dart
class ApiConstants {
  static const String baseUrl = 'http://10.0.2.2:5051'; // Android emulator
  // static const String baseUrl = 'http://localhost:5051'; // iOS simulator
  // static const String baseUrl = 'https://votre-api.com'; // Production

  static const String loginEndpoint = '/api/mobile/auth/login';
  static const String scanEndpoint = '/api/mobile/factures/scan';
  static const String facturesEndpoint = '/api/mobile/factures';
}
```

#### Jour 2 : Services (API, Auth, Storage)

**Fichier :** `lib/services/api_service.dart`

```dart
import 'package:dio/dio.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

class ApiService {
  final Dio _dio = Dio();
  final FlutterSecureStorage _storage = FlutterSecureStorage();

  ApiService() {
    _dio.options.baseUrl = ApiConstants.baseUrl;
    _dio.interceptors.add(InterceptorsWrapper(
      onRequest: (options, handler) async {
        final token = await _storage.read(key: 'jwt_token');
        if (token != null) {
          options.headers['Authorization'] = 'Bearer $token';
        }
        return handler.next(options);
      },
    ));
  }

  Future<Response> login(String email, String password) async {
    return await _dio.post(
      ApiConstants.loginEndpoint,
      data: {'email': email, 'password': password},
    );
  }

  Future<Response> scanInvoice(File image) async {
    final formData = FormData.fromMap({
      'image': await MultipartFile.fromFile(image.path),
    });
    return await _dio.post(ApiConstants.scanEndpoint, data: formData);
  }

  Future<Response> getInvoices() async {
    return await _dio.get(ApiConstants.facturesEndpoint);
  }

  Future<Response> createInvoice(Map<String, dynamic> data) async {
    return await _dio.post(ApiConstants.facturesEndpoint, data: data);
  }
}
```

#### Jour 3 : Écran de Login

**Fichier :** `lib/screens/login_screen.dart`

```dart
class LoginScreen extends StatefulWidget {
  @override
  _LoginScreenState createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  final _apiService = ApiService();
  bool _isLoading = false;

  Future<void> _login() async {
    setState(() => _isLoading = true);

    try {
      final response = await _apiService.login(
        _emailController.text,
        _passwordController.text,
      );

      // Stocker le token
      final storage = FlutterSecureStorage();
      await storage.write(key: 'jwt_token', value: response.data['token']);

      // Naviguer vers le dashboard
      Navigator.pushReplacement(
        context,
        MaterialPageRoute(builder: (_) => DashboardScreen()),
      );
    } catch (e) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Erreur de connexion')),
      );
    } finally {
      setState(() => _isLoading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Padding(
        padding: EdgeInsets.all(24),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Text('EInvoicing', style: TextStyle(fontSize: 32, fontWeight: FontWeight.bold)),
            SizedBox(height: 48),
            TextField(
              controller: _emailController,
              decoration: InputDecoration(labelText: 'Email'),
            ),
            SizedBox(height: 16),
            TextField(
              controller: _passwordController,
              decoration: InputDecoration(labelText: 'Mot de passe'),
              obscureText: true,
            ),
            SizedBox(height: 32),
            ElevatedButton(
              onPressed: _isLoading ? null : _login,
              child: _isLoading
                  ? CircularProgressIndicator()
                  : Text('Se connecter'),
            ),
          ],
        ),
      ),
    );
  }
}
```

#### Jour 4-5 : Écran de Scan

**Fichier :** `lib/screens/scan_screen.dart`

```dart
class ScanScreen extends StatefulWidget {
  @override
  _ScanScreenState createState() => _ScanScreenState();
}

class _ScanScreenState extends State<ScanScreen> {
  final ImagePicker _picker = ImagePicker();
  final ApiService _apiService = ApiService();
  File? _image;
  Map<String, dynamic>? _extractedData;
  bool _isProcessing = false;

  Future<void> _takePicture() async {
    final XFile? photo = await _picker.pickImage(source: ImageSource.camera);
    if (photo != null) {
      setState(() {
        _image = File(photo.path);
      });
      await _processImage();
    }
  }

  Future<void> _processImage() async {
    if (_image == null) return;

    setState(() => _isProcessing = true);

    try {
      final response = await _apiService.scanInvoice(_image!);
      setState(() {
        _extractedData = response.data;
      });
    } catch (e) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Erreur lors du scan')),
      );
    } finally {
      setState(() => _isProcessing = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text('Scanner une facture')),
      body: Column(
        children: [
          if (_image != null)
            Image.file(_image!, height: 300),
          SizedBox(height: 16),
          if (_isProcessing)
            CircularProgressIndicator()
          else if (_extractedData != null)
            Expanded(
              child: ListView(
                padding: EdgeInsets.all(16),
                children: [
                  Text('Données extraites:', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
                  SizedBox(height: 16),
                  _buildDataRow('Numéro', _extractedData!['numero']),
                  _buildDataRow('Client', _extractedData!['clientNom']),
                  _buildDataRow('Total HT', _extractedData!['totalHt']),
                  _buildDataRow('Total TTC', _extractedData!['totalTtc']),
                  SizedBox(height: 24),
                  ElevatedButton(
                    onPressed: _saveInvoice,
                    child: Text('Enregistrer'),
                  ),
                ],
              ),
            ),
        ],
      ),
      floatingActionButton: FloatingActionButton(
        onPressed: _takePicture,
        child: Icon(Icons.camera_alt),
      ),
    );
  }

  Widget _buildDataRow(String label, dynamic value) {
    return Padding(
      padding: EdgeInsets.symmetric(vertical: 8),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label, style: TextStyle(fontWeight: FontWeight.bold)),
          Text(value?.toString() ?? '—'),
        ],
      ),
    );
  }

  Future<void> _saveInvoice() async {
    try {
      await _apiService.createInvoice(_extractedData!);
      Navigator.pop(context);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Facture enregistrée')),
      );
    } catch (e) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Erreur lors de l\'enregistrement')),
      );
    }
  }
}
```

#### Jour 6 : Écran Dashboard

**Fichier :** `lib/screens/dashboard_screen.dart`

```dart
class DashboardScreen extends StatefulWidget {
  @override
  _DashboardScreenState createState() => _DashboardScreenState();
}

class _DashboardScreenState extends State<DashboardScreen> {
  final ApiService _apiService = ApiService();
  List<dynamic> _invoices = [];
  bool _isLoading = true;

  @override
  void initState() {
    super.initState();
    _loadInvoices();
  }

  Future<void> _loadInvoices() async {
    try {
      final response = await _apiService.getInvoices();
      setState(() {
        _invoices = response.data;
        _isLoading = false;
      });
    } catch (e) {
      setState(() => _isLoading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text('Mes Factures')),
      body: _isLoading
          ? Center(child: CircularProgressIndicator())
          : ListView.builder(
              itemCount: _invoices.length,
              itemBuilder: (context, index) {
                final invoice = _invoices[index];
                return ListTile(
                  title: Text(invoice['numero'] ?? 'Sans numéro'),
                  subtitle: Text(invoice['clientNom'] ?? ''),
                  trailing: Text('${invoice['totalTtc']} TND'),
                  onTap: () {
                    // Naviguer vers les détails
                  },
                );
              },
            ),
      floatingActionButton: FloatingActionButton(
        onPressed: () {
          Navigator.push(
            context,
            MaterialPageRoute(builder: (_) => ScanScreen()),
          ).then((_) => _loadInvoices());
        },
        child: Icon(Icons.add),
      ),
    );
  }
}
```

#### Jour 7 : Tests & Corrections

### PHASE 4 : Tests (2 jours)

#### Tests à effectuer :
1. ✅ Login avec compte valide
2. ✅ Login avec compte invalide
3. ✅ Scanner une facture
4. ✅ Extraction des données
5. ✅ Enregistrement dans la base
6. ✅ Affichage dans le dashboard web
7. ✅ Synchronisation mobile ↔ web

### PHASE 5 : Déploiement (1 jour)

#### Build Android
```bash
flutter build apk --release
# APK dans : build/app/outputs/flutter-apk/app-release.apk
```

#### Build iOS
```bash
flutter build ios --release
# Nécessite un Mac et un compte Apple Developer
```

## 📊 Estimation Totale

| Phase | Durée | Difficulté |
|-------|-------|------------|
| Backend API | 2 jours | ⭐⭐ Facile |
| Setup Flutter | 1 jour | ⭐ Très facile |
| Développement Mobile | 7 jours | ⭐⭐⭐ Moyenne |
| Tests | 2 jours | ⭐⭐ Facile |
| Déploiement | 1 jour | ⭐⭐ Facile |
| **TOTAL** | **13 jours** | **Faisable** |

## 🎯 Résultat Final

**Application mobile qui permet de :**
1. ✅ Se connecter avec son compte
2. ✅ Scanner une facture papier
3. ✅ Extraire automatiquement les données (OCR)
4. ✅ Enregistrer dans le dashboard web
5. ✅ Voir toutes ses factures
6. ✅ Synchronisation temps réel

## 💡 Conseils

1. **Commencez par le backend** - C'est le plus important
2. **Testez l'API avec Postman** avant de coder le mobile
3. **Utilisez l'émulateur Android** pour tester rapidement
4. **Gardez le code simple** au début
5. **Ajoutez des fonctionnalités progressivement**

## 🚀 Prochaines Étapes

1. **MAINTENANT** : Créer les endpoints backend mobile
2. **DEMAIN** : Installer Flutter et créer le projet
3. **SEMAINE PROCHAINE** : Développer l'application mobile

## 📚 Ressources

- Flutter : https://flutter.dev
- Dio (HTTP) : https://pub.dev/packages/dio
- Camera : https://pub.dev/packages/camera
- ML Kit : https://pub.dev/packages/google_mlkit_text_recognition

---

**C'est un projet ambitieux mais totalement faisable en 2 semaines ! 🎉**
