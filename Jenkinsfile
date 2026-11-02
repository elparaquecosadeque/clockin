pipeline {
    agent any
    options {
        disableConcurrentBuilds()
        timestamps()
        buildDiscarder(logRotator(numToKeepStr: '30'))
    }
    parameters {
        string(name: 'DEPLOY_TAG', defaultValue: '', description: 'Existing release tag for application rollback; blank builds this commit.')
        string(name: 'FIRST_ADMIN_EMAIL', defaultValue: '', description: 'Required for first production deployment. Use the same email on later releases.')
        booleanParam(name: 'ACK_SCHEMA_COMPATIBLE', defaultValue: false, description: 'Rollback only: confirm target images support the current database schema.')
    }
    stages {
        stage('Test backend') {
            when { expression { !params.DEPLOY_TAG?.trim() } }
            steps {
                sh '''docker run --rm --volumes-from jenkins -v /var/run/docker.sock:/var/run/docker.sock --add-host host.docker.internal:host-gateway -e TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal -w "$WORKSPACE/backend" mcr.microsoft.com/dotnet/sdk:10.0.403 sh -c 'apt-get update -qq && apt-get install -y --no-install-recommends nodejs && dotnet tool restore && dotnet restore && dotnet build --no-restore -p:ContinuousIntegrationBuild=true && dotnet format --verify-no-changes --no-restore && node ../scripts/coverage.mjs backend' '''
            }
        }
        stage('Test frontend') {
            when { expression { !params.DEPLOY_TAG?.trim() } }
            steps {
                sh '''docker run --rm --volumes-from jenkins -e CI=true -w "$WORKSPACE/frontend" node:24-bookworm sh -c 'npm ci && npm run format:check && npm run test:ci && npm run build && npx playwright install --with-deps chromium && npm run e2e' '''
            }
        }
        stage('Deploy') {
            steps {
                script {
                    env.CLOCKIN_TAG = params.DEPLOY_TAG?.trim() ?: "${env.BUILD_NUMBER}-${sh(script: 'git rev-parse --short HEAD', returnStdout: true).trim()}"
                    env.CLOCKIN_FIRST_ADMIN_EMAIL = params.FIRST_ADMIN_EMAIL.trim() ?: env.CLOCKIN_FIRST_ADMIN_EMAIL
                    env.CLOCKIN_ROLLBACK = params.DEPLOY_TAG?.trim() ? 'true' : 'false'
                    env.ACK_SCHEMA_COMPATIBLE = params.ACK_SCHEMA_COMPATIBLE.toString()
                }
                withCredentials([
                    string(credentialsId: 'clockin-postgres-password', variable: 'CLOCKIN_DB_PASSWORD'),
                    string(credentialsId: 'clockin-logging-password', variable: 'CLOCKIN_LOGGING_PASSWORD'),
                    string(credentialsId: 'clockin-signing-key-base64', variable: 'CLOCKIN_SIGNING_KEY_BASE64')
                ]) {
                    sh '''#!/usr/bin/env bash
set -euo pipefail
set +x
export CLOCKIN_SIGNING_KEY="$(printf %s "$CLOCKIN_SIGNING_KEY_BASE64" | base64 -d)"
if [[ "$CLOCKIN_ROLLBACK" == true ]]; then
    [[ "$CLOCKIN_TAG" =~ ^[A-Za-z0-9][A-Za-z0-9_.-]{0,79}$ ]] || exit 1
    bash "/var/jenkins_home/deploy-state/clockin/$CLOCKIN_TAG/scripts/deploy.sh"
else
    bash scripts/deploy.sh
fi
'''
                }
            }
        }
    }
    post {
        always {
            archiveArtifacts artifacts: 'coverage/backend/**,frontend/test-results/**,frontend/playwright-report/**', allowEmptyArchive: true
        }
    }
}
