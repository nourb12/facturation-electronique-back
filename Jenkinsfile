pipeline {
  agent any

  options {
    disableConcurrentBuilds()
    timestamps()
  }

  parameters {
    booleanParam(name: 'RUN_FRONTEND_TESTS', defaultValue: true, description: 'Run Angular/Karma tests in a Chrome-enabled Node image.')
    booleanParam(name: 'RUN_MOBILE', defaultValue: false, description: 'Run Flutter analyze/test. First run downloads a large Flutter image.')
    booleanParam(name: 'RUN_SONAR', defaultValue: true, description: 'Run SonarQube analysis and enforce the Quality Gate.')
    booleanParam(name: 'RUN_SONAR_FRONTEND', defaultValue: false, description: 'Optional lightweight frontend SonarQube scan. Keep disabled on low-memory Docker Desktop setups.')
    booleanParam(name: 'BUILD_LOCAL_IMAGES', defaultValue: true, description: 'Build backend/frontend runtime Docker images from local publish/dist outputs.')
    booleanParam(name: 'DEPLOY_LOCAL', defaultValue: false, description: 'Deploy the local Docker Compose stack after successful build.')
    booleanParam(name: 'PUSH_TO_NEXUS', defaultValue: false, description: 'Push runtime Docker images to the Nexus Docker registry after a successful local image build.')
  }

  environment {
    BACKEND_HOST_PATH = '/host_mnt/c/backendpfe/einvoicing'
    FRONTEND_HOST_PATH = '/host_mnt/c/frontendpfe/ey-invoice-portal'
    MOBILE_HOST_PATH = '/host_mnt/c/mobile/ey_invoice_mobile'

    DOTNET_IMAGE = 'mcr.microsoft.com/dotnet/sdk:8.0'
    DOTNET_SONAR_IMAGE = 'tuniflow-dotnet-sonar:8.0'
    NODE_IMAGE = 'node:20-bookworm'
    CHROME_NODE_IMAGE = 'cypress/browsers@sha256:1e78e0a7417f9491832f5c5e3fbe3b0b74b4d3270e38de958237f73e9de9b728'
    FLUTTER_IMAGE = 'ghcr.io/cirruslabs/flutter:stable'
    SONAR_SCANNER_IMAGE = 'sonarsource/sonar-scanner-cli:5.0'
    SONAR_CURL_IMAGE = 'curlimages/curl:8.11.1'
    SONAR_HOST_URL = 'http://tuniflow-sonarqube:9000'
    SONAR_NETWORK = 'tuniflow-platform'
    NEXUS_REGISTRY = 'localhost:8085'
    VERIFY_BASE_URL = 'http://host.docker.internal'
    SONAR_BACKEND_EXCLUSIONS = '**/.git/**,**/.vs/**,**/.docker/**,**/.sonarqube/**,**/bin/**,**/obj/**,**/ocr_service/**,**/.venv/**,**/__pycache__/**,**/uploads/**,**/TestResults/**'
    SONAR_BACKEND_COVERAGE_EXCLUSIONS = '**/Controllers/**,**/DTOs/**,**/Entities/**,**/Enums/**,**/Program.cs,**/Migrations/**'
    DOTNET_DOCKER_ENV = '-e NUGET_PACKAGES=/src/.docker/ci-cache/nuget -e DOTNET_CLI_HOME=/tmp'
    FRONTEND_DOCKER_VOLUMES = '-v tuniflow_frontend_node_modules:/app/node_modules -v tuniflow_frontend_npm_cache:/root/.npm'
  }

  stages {
    stage('Backend Restore') {
      steps {
        sh '''
          set -eux
          mkdir -p "$BACKEND_HOST_PATH/.docker/ci-cache/nuget"
          docker run --rm $DOTNET_DOCKER_ENV -v "$BACKEND_HOST_PATH:/src" -w /src "$DOTNET_IMAGE" \
            dotnet restore Einvoicing.sln
        '''
      }
    }

    stage('Backend Build') {
      steps {
        sh '''
          set -eux
          docker run --rm $DOTNET_DOCKER_ENV -v "$BACKEND_HOST_PATH:/src" -w /src "$DOTNET_IMAGE" \
            dotnet build Einvoicing.sln --configuration Release --no-restore
        '''
      }
    }

    stage('Backend Tests') {
      steps {
        sh '''
          set -eux
          docker run --rm $DOTNET_DOCKER_ENV -v "$BACKEND_HOST_PATH:/src" -w /src "$DOTNET_IMAGE" \
            dotnet test Einvoicing.sln --configuration Release --no-restore --logger "trx;LogFileName=backend-tests.trx"
        '''
      }
    }

    stage('Frontend Install') {
      steps {
        sh '''
          set -eux
          docker run --rm -v "$FRONTEND_HOST_PATH:/app" $FRONTEND_DOCKER_VOLUMES -w /app "$NODE_IMAGE" npm ci
        '''
      }
    }

    stage('Frontend Tests') {
      when { expression { return params.RUN_FRONTEND_TESTS } }
      steps {
        sh '''
          set -eux
          docker run --rm -e CHROME_BIN=/usr/bin/google-chrome \
            -v "$FRONTEND_HOST_PATH:/app" $FRONTEND_DOCKER_VOLUMES -w /app "$CHROME_NODE_IMAGE" \
            npm test -- --watch=false --browsers=ChromeHeadlessCI --code-coverage
        '''
      }
    }

    stage('Frontend Build') {
      steps {
        sh '''
          set -eux
          docker run --rm -v "$FRONTEND_HOST_PATH:/app" $FRONTEND_DOCKER_VOLUMES -w /app "$NODE_IMAGE" \
            npm run build -- --configuration production
        '''
      }
    }

    stage('Sonar Backend') {
      when { expression { return params.RUN_SONAR } }
      steps {
        withCredentials([string(credentialsId: 'sonarqube-token', variable: 'SONAR_TOKEN')]) {
          sh '''
            set -eu
            docker build -t "$DOTNET_SONAR_IMAGE" -f "$BACKEND_HOST_PATH/devops/sonar/dotnet-sonar.Dockerfile" "$BACKEND_HOST_PATH"
            docker rm -f tuniflow-sonar-backend >/dev/null 2>&1 || true
            docker run --rm --network "$SONAR_NETWORK" $DOTNET_DOCKER_ENV \
              --name tuniflow-sonar-backend \
              -e SONAR_HOST_URL="$SONAR_HOST_URL" \
              -e SONAR_TOKEN="$SONAR_TOKEN" \
              -e SONAR_BACKEND_EXCLUSIONS="$SONAR_BACKEND_EXCLUSIONS" \
              -e SONAR_BACKEND_COVERAGE_EXCLUSIONS="$SONAR_BACKEND_COVERAGE_EXCLUSIONS" \
              -v "$BACKEND_HOST_PATH:/src" -w /src "$DOTNET_SONAR_IMAGE" \
              sh -lc 'set -eu
                rm -rf .sonarqube
                /root/.dotnet/tools/dotnet-sonarscanner begin \
                  /k:"tuniflow-backend" \
                  /n:"TuniFlow Backend" \
                  /d:sonar.host.url="$SONAR_HOST_URL" \
                  /d:sonar.token="$SONAR_TOKEN" \
                  /d:sonar.projectBaseDir=/src \
                  /d:sonar.scanner.scanAll=false \
                  /d:sonar.exclusions="$SONAR_BACKEND_EXCLUSIONS" \
                  /d:sonar.coverage.exclusions="$SONAR_BACKEND_COVERAGE_EXCLUSIONS" \
                  /d:sonar.cs.vstest.reportsPaths="/src/TestResults/*.trx" \
                  /d:sonar.cs.opencover.reportsPaths="/src/TestResults/**/coverage.opencover.xml"
                dotnet build Einvoicing.sln --configuration Release --no-restore
                rm -rf /src/TestResults
                mkdir -p /src/TestResults
                dotnet test Einvoicing.Tests/Einvoicing.Tests.csproj --configuration Release --no-build \
                  --logger "trx;LogFileName=backend-sonar-tests.trx" \
                  --results-directory /src/TestResults \
                  --collect:"XPlat Code Coverage;Format=opencover"
                /root/.dotnet/tools/dotnet-sonarscanner end /d:sonar.token="$SONAR_TOKEN"
              '
          '''
        }
      }
    }

    stage('Sonar Frontend') {
      when { expression { return params.RUN_SONAR && params.RUN_SONAR_FRONTEND } }
      steps {
        withCredentials([string(credentialsId: 'sonarqube-token', variable: 'SONAR_TOKEN')]) {
          sh '''
            set -eu
            rm -rf "$FRONTEND_HOST_PATH/.scannerwork"
            docker run --rm --network "$SONAR_NETWORK" \
              -e SONAR_HOST_URL="$SONAR_HOST_URL" \
              -e SONAR_TOKEN="$SONAR_TOKEN" \
              -e NODE_OPTIONS=--max-old-space-size=4096 \
              -v "$FRONTEND_HOST_PATH:/usr/src" \
              "$SONAR_SCANNER_IMAGE" \
              -Dsonar.projectKey=tuniflow-frontend \
              -Dsonar.projectName="TuniFlow Frontend" \
              -Dsonar.projectBaseDir=/usr/src \
              -Dsonar.working.directory=.scannerwork \
              -Dsonar.sourceEncoding=UTF-8 \
              -Dsonar.nodejs.executable=/usr/bin/node \
              -Dsonar.javascript.node.maxspace=4096 \
              -Dsonar.sources=src \
              -Dsonar.tests=src \
              -Dsonar.test.inclusions=**/*.spec.ts \
              -Dsonar.typescript.tsconfigPaths=tsconfig.json,tsconfig.app.json,tsconfig.spec.json \
              -Dsonar.scm.disabled=true \
              -Dsonar.exclusions=**/node_modules/**,**/dist/**,**/.angular/**,**/coverage/**,src/assets/**
          '''
        }
      }
    }

    stage('Sonar Quality Gate') {
      when { expression { return params.RUN_SONAR } }
      steps {
        withCredentials([string(credentialsId: 'sonarqube-token', variable: 'SONAR_TOKEN')]) {
          sh '''
            set -eu

            wait_quality_gate() {
              report_file="$1"
              label="$2"

              if [ ! -f "$report_file" ]; then
                echo "$label Sonar report-task.txt not found: $report_file"
                exit 1
              fi

              ceTaskId="$(sed -n 's/^ceTaskId=//p' "$report_file" | head -n 1)"
              if [ -z "$ceTaskId" ]; then
                echo "$label Sonar ceTaskId not found in $report_file"
                exit 1
              fi

              analysisId=""
              for attempt in $(seq 1 60); do
                response="$(docker run --rm --network "$SONAR_NETWORK" "$SONAR_CURL_IMAGE" -sS -u "$SONAR_TOKEN:" "$SONAR_HOST_URL/api/ce/task?id=$ceTaskId")"
                status="$(printf '%s' "$response" | sed -n 's/.*"status":"\\([^"]*\\)".*/\\1/p')"
                echo "$label Sonar compute engine status: ${status:-UNKNOWN}"

                if [ "$status" = "SUCCESS" ]; then
                  analysisId="$(printf '%s' "$response" | sed -n 's/.*"analysisId":"\\([^"]*\\)".*/\\1/p')"
                  break
                fi

                if [ "$status" = "FAILED" ] || [ "$status" = "CANCELED" ]; then
                  echo "$response"
                  exit 1
                fi

                sleep 5
              done

              if [ -z "$analysisId" ]; then
                echo "$label Sonar analysis did not finish before timeout."
                exit 1
              fi

              gate_response="$(docker run --rm --network "$SONAR_NETWORK" "$SONAR_CURL_IMAGE" -sS -u "$SONAR_TOKEN:" "$SONAR_HOST_URL/api/qualitygates/project_status?analysisId=$analysisId")"
              gate="$(printf '%s' "$gate_response" | sed -n 's/.*"projectStatus":{"status":"\\([^"]*\\)".*/\\1/p')"
              echo "$label Sonar quality gate: ${gate:-UNKNOWN}"

              if [ "$gate" != "OK" ]; then
                echo "$gate_response"
                exit 1
              fi
            }

            wait_quality_gate "$BACKEND_HOST_PATH/.sonarqube/out/.sonar/report-task.txt" "Backend"

            if [ "${RUN_SONAR_FRONTEND:-false}" = "true" ]; then
              wait_quality_gate "$FRONTEND_HOST_PATH/.scannerwork/report-task.txt" "Frontend"
            else
              echo "Frontend Sonar quality gate skipped because RUN_SONAR_FRONTEND=false."
            fi
          '''
        }
      }
    }

    stage('Mobile Analyze') {
      when { expression { return params.RUN_MOBILE } }
      steps {
        sh '''
          set -eux
          docker run --rm -v "$MOBILE_HOST_PATH:/workspace" -w /workspace "$FLUTTER_IMAGE" \
            sh -lc "flutter pub get && flutter analyze --no-pub"
        '''
      }
    }

    stage('Mobile Tests') {
      when { expression { return params.RUN_MOBILE } }
      steps {
        sh '''
          set -eux
          docker run --rm -v "$MOBILE_HOST_PATH:/workspace" -w /workspace "$FLUTTER_IMAGE" \
            flutter test -r compact
        '''
      }
    }

    stage('Prepare Runtime Docker Artifacts') {
      when { expression { return params.BUILD_LOCAL_IMAGES } }
      steps {
        sh '''
          set -eux
          docker run --rm $DOTNET_DOCKER_ENV -v "$BACKEND_HOST_PATH:/src" -w /src "$DOTNET_IMAGE" \
            dotnet publish Einvoicing.Api/Einvoicing.Api.csproj -c Release -o .docker/backend-publish /p:UseAppHost=false
          docker run --rm -v "$FRONTEND_HOST_PATH:/app" $FRONTEND_DOCKER_VOLUMES -w /app "$NODE_IMAGE" \
            npm run build -- --configuration production
        '''
      }
    }

    stage('Docker Build Runtime Images') {
      when { expression { return params.BUILD_LOCAL_IMAGES } }
      steps {
        sh '''
          set -eux
          cd "$BACKEND_HOST_PATH"
          docker compose -f docker-compose.local.yml build backend-api frontend
        '''
      }
    }

    stage('Push Runtime Images To Nexus') {
      when { expression { return params.BUILD_LOCAL_IMAGES && params.PUSH_TO_NEXUS } }
      steps {
        withCredentials([usernamePassword(credentialsId: 'nexus-docker', usernameVariable: 'NEXUS_USER', passwordVariable: 'NEXUS_PASSWORD')]) {
          sh '''
            set -eux
            echo "$NEXUS_PASSWORD" | docker login "$NEXUS_REGISTRY" --username "$NEXUS_USER" --password-stdin
            docker tag tuniflow-backend-api:latest "$NEXUS_REGISTRY/tuniflow-backend-api:latest"
            docker tag tuniflow-frontend:latest "$NEXUS_REGISTRY/tuniflow-frontend:latest"
            docker push "$NEXUS_REGISTRY/tuniflow-backend-api:latest"
            docker push "$NEXUS_REGISTRY/tuniflow-frontend:latest"
          '''
        }
      }
    }

    stage('Deploy Local Docker Stack') {
      when { expression { return params.DEPLOY_LOCAL } }
      steps {
        sh '''
          set -eux
          cd "$BACKEND_HOST_PATH"
          docker compose -f docker-compose.local.yml up -d
          docker compose -f docker-compose.local.yml ps
        '''
      }
    }

    stage('Verify Local Deployment') {
      when { expression { return params.DEPLOY_LOCAL } }
      steps {
        sh '''
          set -eu
          cd "$BACKEND_HOST_PATH"

          check_url() {
            name="$1"
            url="$2"
            expected="$3"

            for attempt in $(seq 1 20); do
              status="$(curl -s -o /dev/null -w "%{http_code}" "$url" || true)"
              if [ "$status" = "$expected" ]; then
                echo "$name OK ($status)"
                return 0
              fi
              echo "$name not ready yet ($status), retry $attempt/20"
              sleep 3
            done

            echo "$name failed, expected $expected"
            exit 1
          }

          docker compose -f docker-compose.local.yml ps
          docker exec tuniflow-postgres pg_isready -U "${POSTGRES_USER:-postgres}" -d "${POSTGRES_DB:-einvoicing_db}"
          check_url "Frontend" "$VERIFY_BASE_URL/" "200"
          check_url "Frontend SPA route" "$VERIFY_BASE_URL/login" "200"
          check_url "OCR Health" "$VERIFY_BASE_URL:8000/health" "200"
          check_url "Backend direct" "$VERIFY_BASE_URL:5051/" "401"
          check_url "Backend proxy login endpoint" "$VERIFY_BASE_URL/api/auth/login" "401"
        '''
      }
    }
  }

  post {
    always {
      sh '''
        set +e
        if [ -d "$BACKEND_HOST_PATH" ]; then
          cd "$BACKEND_HOST_PATH" && docker compose -f docker-compose.local.yml ps || true
        fi
      '''
    }
    success {
      echo 'TuniFlow CI pipeline completed successfully.'
    }
    failure {
      echo 'TuniFlow CI pipeline failed. Check the stage logs above.'
    }
  }
}
