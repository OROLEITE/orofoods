#!/usr/bin/env bash

set -euo pipefail

usage() {
    echo "Usage: $0 --host HOST --user USER --version VERSION --confirm [--service orofoods]" >&2
}

host=""
user=""
version=""
service="orofoods"
confirmed=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --host) host="${2:-}"; shift 2 ;;
        --user) user="${2:-}"; shift 2 ;;
        --version) version="${2:-}"; shift 2 ;;
        --service) service="${2:-}"; shift 2 ;;
        --confirm) confirmed=true; shift ;;
        *) usage; exit 2 ;;
    esac
done

if [[ -z "$host" || -z "$user" || -z "$version" || "$confirmed" != true ]]; then
    usage
    exit 2
fi

artifact_dir="artifacts/orofoods-$version"
remote_dir="/var/www/orofoods"

dotnet restore Orofoods.slnx
dotnet build Orofoods.slnx --no-restore --configuration Release
dotnet test Orofoods.Web.Tests/Orofoods.Web.Tests.csproj --no-restore --configuration Release
dotnet publish Orofoods.Web/Orofoods.Web.csproj --no-restore --configuration Release --output "$artifact_dir"

scp -r "$artifact_dir/" "$user@$host:$remote_dir"
ssh "$user@$host" "sudo systemctl restart '$service'"

echo "Deployed version $version to the authorized test VM: $host"