# Create a new Release, auto incremented
param(
    [string]$version,
    [string]$appName = "",
    [switch]$TriggerWorkflow
)

$versionFile = ".version"

# ✅ Function: Increment patch version
function Increment-Version($ver) {
    if ($ver -match "^v(\d+)\.(\d+)\.(\d+)$") {
        $major = [int]$Matches[1]
        $minor = [int]$Matches[2]
        $patch = [int]$Matches[3]

        $patch++

        return "v$major.$minor.$patch"
    } else {
        Write-Error "Invalid version format. Expected vX.Y.Z"
        exit 1
    }
}

# ✅ Step 1: Determine version
if ($version) {
    Write-Host "Using provided version: $version"
}
else {
    if (Test-Path $versionFile) {
        $lastVersion = (Get-Content $versionFile -Raw).Trim()
        Write-Host "Last version found: $lastVersion"

        $version = Increment-Version $lastVersion
        Write-Host "Auto-incremented version: $version"
    }
    else {
        $version = "v1.0.0"
        Write-Host "No version file found. Starting at $version"
    }
}

# ✅ Step 2..4: Create a unique tag and push it
# If the tag exists locally or on the remote, auto-increment and retry.
$maxAttempts = 20
$attempt = 0
$remoteName = "origin"

while ($attempt -lt $maxAttempts) {
    $attempt++

    # ensure local tag doesn't already exist
    $localExisting = git tag --list $version
    if ($localExisting) {
        Write-Host "Local tag $version already exists — incrementing..."
        $version = Increment-Version $version
        continue
    }

    # check remote for existing tag
    $remoteExisting = git ls-remote --tags $remoteName "refs/tags/$version" 2>$null
    if ($remoteExisting) {
        Write-Host "Remote already has tag $version — auto-incrementing..."
        $version = Increment-Version $version
        continue
    }

    # create local tag
    Write-Host "Creating tag $version..."
    git tag $version

    # attempt to push
    Write-Host "Pushing tag to $remoteName..."
    git push $remoteName $version
    $exit = $LASTEXITCODE

    if ($exit -eq 0) {
        Write-Host "✅ Release triggered for $version"
        # persist version only after successful push
        Set-Content $versionFile $version
        
        # Optionally trigger the release workflow with an app_name input so the workflow
        # receives the chosen application name. This uses the GitHub CLI if available,
        # otherwise falls back to the REST API (requires GITHUB_TOKEN or GITHUB_PAT env).
        if ($TriggerWorkflow.IsPresent) {
            if ([string]::IsNullOrWhiteSpace($appName)) {
                Write-Host "TriggerWorkflow requested but no appName provided — skipping workflow_dispatch."
            }
            else {
                $gh = Get-Command gh -ErrorAction SilentlyContinue
                if ($gh) {
                    Write-Host "Triggering workflow via gh CLI (release.yml) with app_name=$appName and ref=$version..."
                    try {
                        gh workflow run release.yml --ref $version -f app_name=$appName
                        Write-Host "Workflow dispatch sent via gh CLI."
                    }
                    catch {
                        Write-Warning "gh CLI failed to dispatch workflow: $_. Exception.Message"
                    }
                }
                else {
                    # Fallback to GitHub REST API
                    Write-Host "gh CLI not found — attempting REST API dispatch."
                    $remoteUrl = (git remote get-url origin) -as [string]
                    if (-not $remoteUrl) {
                        Write-Warning "Cannot determine origin remote URL — skipping REST dispatch."
                    }
                    else {
                        # Parse owner/repo from remote URL
                        if ($remoteUrl -match 'github.com[:/](?<owner>[^/]+)/(?<repo>[^/.]+)') {
                            $owner = $Matches['owner']
                            $repo = $Matches['repo']
                            $apiUrl = "https://api.github.com/repos/$owner/$repo/actions/workflows/release.yml/dispatches"
                            $token = $env:GITHUB_TOKEN
                            if (-not $token) { $token = $env:GITHUB_PAT }
                            if (-not $token) {
                                Write-Warning "No GITHUB_TOKEN or GITHUB_PAT found in environment — cannot call REST API."
                            }
                            else {
                                $payload = @{ ref = $version; inputs = @{ app_name = $appName } } | ConvertTo-Json
                                try {
                                    Invoke-RestMethod -Method Post -Uri $apiUrl -Headers @{ Authorization = "token $token"; 'User-Agent' = 'release.ps1' } -Body $payload -ContentType 'application/json'
                                    Write-Host "Workflow dispatch sent via REST API."
                                }
                                catch {
                                    Write-Warning "REST API dispatch failed: $_"
                                }
                            }
                        }
                        else {
                            Write-Warning "Could not parse owner/repo from remote URL: $remoteUrl"
                        }
                    }
                }
            }
        }
        exit 0
    }

    # push failed — remove local tag and retry
    Write-Warning "Failed to push tag $version (exit code $exit). Removing local tag and retrying..."
    git tag -d $version 2>$null

    # if remote now has the tag (race), increment and retry
    $remoteExistingNow = git ls-remote --tags $remoteName "refs/tags/$version" 2>$null
    if ($remoteExistingNow) {
        $version = Increment-Version $version
        continue
    }

    # otherwise give up with error
    Write-Error "Failed to push tag $version and remote does not report the tag. Aborting."
    exit 1
}

Write-Error "Exceeded maximum attempts ($maxAttempts) trying to find a unique version tag. Aborting."
exit 1