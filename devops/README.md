# TuniFlow DevOps Guide

Ce document explique comment lancer et utiliser la stack locale TuniFlow:

- application locale: PostgreSQL + OCR + backend + frontend
- stack DevOps: Jenkins + SonarQube + Nexus + Prometheus + Grafana

## Emplacement du projet

Backend / DevOps:

```powershell
C:\backendpfe\einvoicing
```

Frontend:

```powershell
C:\frontendpfe\ey-invoice-portal
```

Mobile:

```powershell
C:\mobile\ey_invoice_mobile
```

## Fichiers importants

- `docker-compose.local.yml` : lance l'application locale
- `docker-compose.devops.yml` : lance la stack DevOps
- `.env` : variables locales backend / Docker local
- `.env.example` : modele de variables
- `devops/.env.devops` : variables locales de la stack DevOps
- `devops/.env.devops.example` : modele de variables DevOps
- `Jenkinsfile` : pipeline CI/CD
- `devops/verify-stack.ps1` : verification automatique de la stack

## Prerequis

Avant de lancer la stack:

1. Docker Desktop doit etre demarre
2. Le fichier `.env` doit exister a la racine du backend
3. Le fichier `devops/.env.devops` doit exister
3. Le reseau Docker partage `tuniflow-platform` doit exister

Si besoin, preparer les fichiers de variables une seule fois:

```powershell
Copy-Item .env.example .env
Copy-Item .\devops\.env.devops.example .\devops\.env.devops
```

Puis remplacer les valeurs `CHANGE_ME`.

Si besoin, creer le reseau une seule fois:

```powershell
docker network create tuniflow-platform
```

## Ordre logique d'utilisation

Ordre recommande:

1. lancer l'application locale
2. verifier que frontend / backend / OCR / PostgreSQL fonctionnent
3. lancer la stack DevOps
4. ouvrir Jenkins
5. lancer le pipeline
6. consulter SonarQube
7. consulter Nexus
8. consulter Prometheus / Grafana

## 1. Lancer l'application locale

Depuis:

```powershell
cd C:\backendpfe\einvoicing
```

Commande:

```powershell
docker compose -f docker-compose.local.yml up -d
```

Verifier les conteneurs:

```powershell
docker compose -f docker-compose.local.yml ps
```

Arreter l'application locale:

```powershell
docker compose -f docker-compose.local.yml down
```

## 2. Lancer la stack DevOps

Depuis:

```powershell
cd C:\backendpfe\einvoicing
```

Commande:

```powershell
docker compose -f docker-compose.devops.yml up -d
```

Verifier les conteneurs:

```powershell
docker compose -f docker-compose.devops.yml ps
```

Verifier rapidement la stack complete:

```powershell
powershell -ExecutionPolicy Bypass -File .\devops\verify-stack.ps1
```

Arreter la stack DevOps:

```powershell
docker compose -f docker-compose.devops.yml down
```

## URLs utiles

### Application locale

- Frontend: [http://localhost](http://localhost)
- Frontend login: [http://localhost/login](http://localhost/login)
- Backend API direct: [http://localhost:5051](http://localhost:5051)
- OCR API: [http://localhost:8000](http://localhost:8000)
- OCR health: [http://localhost:8000/health](http://localhost:8000/health)
- PostgreSQL: `localhost:5432`

### Stack DevOps

- Jenkins: [http://localhost:8081](http://localhost:8081)
- SonarQube: [http://localhost:9000](http://localhost:9000)
- Nexus UI: [http://localhost:8083](http://localhost:8083)
- Nexus Docker Registry: `localhost:8085`
- Prometheus: [http://localhost:9090](http://localhost:9090)
- Grafana: [http://localhost:3000](http://localhost:3000)
- Dashboard Grafana TuniFlow: [http://localhost:3000/d/tuniflow-overview/tuniflow-overview](http://localhost:3000/d/tuniflow-overview/tuniflow-overview)

## Logins / acces

### Jenkins

Cas normal:

- utiliser le compte configure pendant la premiere initialisation Jenkins

Si Jenkins vient d'etre recree ou si c'est un premier lancement:

```powershell
docker exec tuniflow-jenkins cat /var/jenkins_home/secrets/initialAdminPassword
```

Puis ouvrir:

- [http://localhost:8081](http://localhost:8081)

### Grafana

Valeurs locales:

- user: valeur de `GRAFANA_ADMIN_USER`
- password: valeur de `GRAFANA_ADMIN_PASSWORD`

dans:

- `devops/.env.devops`

### SonarQube

Par defaut au premier lancement:

- user: `admin`
- password: `admin`

Puis SonarQube peut demander de changer le mot de passe.

### Nexus

Utilisateur initial:

- user: `admin`

Mot de passe initial:

```powershell
docker exec tuniflow-nexus cat /nexus-data/admin.password
```

Note:

- ce fichier existe surtout au premier lancement
- apres configuration initiale Nexus, utilisez le mot de passe defini dans l'interface

Puis ouvrir:

- [http://localhost:8083](http://localhost:8083)

### PostgreSQL local

Valeurs locales par defaut si `.env` n'a pas ete modifie:

- host: `localhost`
- port: `5432`
- database: `einvoicing_db`
- user: `postgres`
- password: valeur de `POSTGRES_PASSWORD` dans `.env`

## Premiere mise en route recommandee

### Etape A - application locale

```powershell
cd C:\backendpfe\einvoicing
docker compose -f docker-compose.local.yml up -d
```

Verifier:

- [http://localhost](http://localhost)
- [http://localhost/login](http://localhost/login)
- [http://localhost:8000/health](http://localhost:8000/health)

### Etape B - stack DevOps

```powershell
cd C:\backendpfe\einvoicing
docker compose -f docker-compose.devops.yml up -d
```

Verifier:

- [http://localhost:8081](http://localhost:8081)
- [http://localhost:9000](http://localhost:9000)
- [http://localhost:8083](http://localhost:8083)
- [http://localhost:9090](http://localhost:9090)
- [http://localhost:3000](http://localhost:3000)

## Jenkins - quoi lancer

Le pipeline `tuniflow-ci` peut etre lance avec parametres.

### Run complet utile

- `RUN_FRONTEND_TESTS=true`
- `RUN_SONAR=true`
- `RUN_SONAR_FRONTEND=true`
- `BUILD_LOCAL_IMAGES=true`
- `PUSH_TO_NEXUS=true`
- `DEPLOY_LOCAL=true`

### Run plus rapide pour simple verification locale

- `RUN_FRONTEND_TESTS=true`
- `RUN_SONAR=false`
- `BUILD_LOCAL_IMAGES=false`
- `PUSH_TO_NEXUS=false`
- `DEPLOY_LOCAL=true`

## Credentials Jenkins importantes

Les credentials suivantes doivent exister dans Jenkins:

### SonarQube

- id: `sonarqube-token`
- type: `Secret text`

### Nexus Docker

- id: `nexus-docker`
- type: `Username with password`

## Nexus - repository Docker attendu

Dans Nexus, il faut un repository:

- type: `docker (hosted)`
- nom conseille: `tuniflow-docker`
- port HTTP: `8085`
- deployment policy: `Allow redeploy`

Controle rapide attendu:

- URL registry: `localhost:8085`
- push Jenkins vers:
  - `localhost:8085/tuniflow-backend-api:latest`
  - `localhost:8085/tuniflow-frontend:latest`

## Monitoring

### Prometheus

Prometheus surveille:

- backend metrics
- frontend via blackbox
- backend health via blackbox
- OCR health via blackbox
- Jenkins via blackbox
- SonarQube via blackbox
- Nexus via blackbox
- PostgreSQL exporter
- node-exporter
- cAdvisor

### Grafana

Grafana est provisionne automatiquement avec:

- datasource `Prometheus`
- folder `TuniFlow`
- dashboard `TuniFlow Overview`

Verification attendue:

- datasource `Prometheus` en etat `OK`
- dashboard charge sans erreur
- targets Prometheus toutes `UP`

## Commandes utiles

Voir tous les conteneurs TuniFlow:

```powershell
docker ps
```

Voir les logs d'un service local:

```powershell
docker logs tuniflow-backend-api --tail 200
docker logs tuniflow-frontend --tail 200
docker logs tuniflow-ocr-api --tail 200
```

Voir les logs DevOps:

```powershell
docker logs tuniflow-jenkins --tail 200
docker logs tuniflow-sonarqube --tail 200
docker logs tuniflow-nexus --tail 200
docker logs tuniflow-prometheus --tail 200
docker logs tuniflow-grafana --tail 200
```

Verifier toute la stack automatiquement:

```powershell
cd C:\backendpfe\einvoicing
powershell -ExecutionPolicy Bypass -File .\devops\verify-stack.ps1
```

## Resume rapide

Pour travailler normalement:

```powershell
cd C:\backendpfe\einvoicing
docker compose -f docker-compose.local.yml up -d
docker compose -f docker-compose.devops.yml up -d
```

Ensuite ouvrir:

- [http://localhost](http://localhost)
- [http://localhost:8081](http://localhost:8081)
- [http://localhost:9000](http://localhost:9000)
- [http://localhost:8083](http://localhost:8083)
- [http://localhost:9090](http://localhost:9090)
- [http://localhost:3000](http://localhost:3000)

Puis verifier:

```powershell
powershell -ExecutionPolicy Bypass -File .\devops\verify-stack.ps1
```
