# Jenkins TuniFlow

Jenkins est lance via `docker-compose.devops.yml` sur le port 8081.

## Demarrage

```powershell
cd C:\backendpfe\einvoicing
docker compose -f docker-compose.devops.yml up -d jenkins
```

## Mot de passe initial

```powershell
docker exec tuniflow-jenkins cat /var/jenkins_home/secrets/initialAdminPassword
```

## Pipeline

Le `Jenkinsfile` a ete prepare pour un Jenkins Docker local. Il utilise le Docker CLI dans le conteneur Jenkins pour lancer des conteneurs de build .NET, Node/Chrome et Flutter.

La premiere execution peut etre lente, car Docker doit telecharger les images de build.