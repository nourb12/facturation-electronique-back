# facturation-electronique-back

## Configuration locale des secrets

Ne pas commiter `Einvoicing.Api/appsettings.Development.json`: ce fichier est specifique a la machine locale et ignore par Git.

Initialiser les secrets utilisateur une seule fois:

```powershell
dotnet user-secrets init --project .\Einvoicing.Api\Einvoicing.Api.csproj
```

Configurer les valeurs sensibles en local:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=einvoicing_db;Username=postgres;Password=VOTRE_MOT_DE_PASSE" --project .\Einvoicing.Api\Einvoicing.Api.csproj
dotnet user-secrets set "Jwt:Secret" "REMPLACER_PAR_UN_SECRET_LOCAL_LONG_AU_MOINS_32_CARACTERES" --project .\Einvoicing.Api\Einvoicing.Api.csproj
dotnet user-secrets set "SuperAdmin:Password" "REMPLACER_PAR_UN_MOT_DE_PASSE_ADMIN_LOCAL" --project .\Einvoicing.Api\Einvoicing.Api.csproj
```

Optionnel, seulement si l'envoi reel est requis en developpement:

```powershell
dotnet user-secrets set "Email:Utilisateur" "votre-compte-smtp" --project .\Einvoicing.Api\Einvoicing.Api.csproj
dotnet user-secrets set "Email:MotDePasse" "votre-secret-smtp" --project .\Einvoicing.Api\Einvoicing.Api.csproj
dotnet user-secrets set "Sms:ApiKey" "votre-api-key" --project .\Einvoicing.Api\Einvoicing.Api.csproj
dotnet user-secrets set "Sms:ApiSecret" "votre-api-secret" --project .\Einvoicing.Api\Einvoicing.Api.csproj
```

`Email:MotDePasse = DEMO` garde le service email en mode developpement sans envoi SMTP reel.
