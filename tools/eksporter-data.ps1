#Requires -Version 7.0
<#
.SYNOPSIS
    Eksporterer katalog- og detaljdata fra en kjørende lokal RegelIde.Api til nettside/data/.

.DESCRIPTION
    Henter data via Invoke-RestMethod og skriver JSON-filer i katalog-/detalj-strukturen som
    nettside/data/README.md dokumenterer. Trygt å kjøre på nytt — hver fil overskrives i sin
    helhet (aldri append), så to kjøringer mot samme API gir samme resultat.

    Scriptet gjør ALDRI skriving mot API-et — kun GET-kall. Det leser fra src/RegelIde.Api, det
    endrer det aldri.

.PARAMETER BaseUrl
    Rot-URL-en til den kjørende RegelIde.Api-instansen. Default matcher "https"-profilen i
    src/RegelIde.Api/Properties/launchSettings.json.

.PARAMETER BrukerId
    X-Bruker-Id-header brukt mot endepunkter som krever en innlogget testbruker (kun
    GET /api/begreper i dette scriptet). Default er "Kari Jurist" (samme bruker
    SamiskSprakforvaltningSeed kjører som), fra GET /api/brukere i et ferskt dev-miljø.
    Overstyr med en annen bruker-id fra ditt eget miljø om denne ikke finnes.

.EXAMPLE
    ./eksporter-data.ps1
    Eksporterer mot https://localhost:7010 (default).

.EXAMPLE
    ./eksporter-data.ps1 -BaseUrl http://localhost:5187
    Eksporterer mot http-profilen i stedet.
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = "https://localhost:7010",
    [string]$BrukerId = "33c42690-e92c-4b09-b9ff-960963c13240"
)

$ErrorActionPreference = "Stop"

# nettside/data, uansett hvor scriptet kjøres fra
$DataRoot = Join-Path (Split-Path -Parent $PSScriptRoot) "data"

# ---------------------------------------------------------------------------------------------
# Startutvalget for detaljfiler (docs: nettside/data/README.md). Legg til flere id-er her for å
# utvide utvalget — se README.md punkt "Slik legger du til en ny detaljfil".
# ---------------------------------------------------------------------------------------------
$VirksomhetIderForDetalj = @(
    "455d90bf-3b8e-4d75-a735-b6b2768d17fa"  # Karasjok kommune (orgnr 963376030)
)
$GruppeIderForDetalj = @(
    "7057ff4b-bc2e-4d23-af7b-c681a082ea32", # forvaltningsområdet for samiske språk
    "52dcc48d-d228-42d6-8bb1-d9397e29b734", # språkutviklingskommuner
    "ea37d4ce-b7a2-439b-91b0-b79920759ddf", # språkvitaliseringskommuner
    "86eefdfb-3c5f-46e4-80ec-95ea5dc51dd8"  # språkstimuleringskommuner
)
$BegrepIderForDetalj = @(
    "0fe408ce-125b-480c-a17d-b04b44bc70cc", # kommunens skjønnsutøvelse ved bevilling (alkoholloven § 1-7a)
    "c98ed4b3-fb01-4159-ae14-fe4cbd9a378d", # skjenketid (alkoholloven § 4-4)
    "a7fbcd03-96c4-4216-82c6-60d602bf0543", # styrer og stedfortreder (alkoholloven § 1-7c)
    "e9c77642-1c56-4152-bf60-477ea598d7b1"  # uklanderlig vandel (alkoholloven § 1-7b)
)

function Get-Api {
    param([string]$Sti, [switch]$MedBrukerHeader)
    $uri = "$BaseUrl$Sti"
    $headers = @{}
    if ($MedBrukerHeader) { $headers["X-Bruker-Id"] = $BrukerId }
    Write-Host "  GET $Sti" -ForegroundColor DarkGray
    Invoke-RestMethod -Uri $uri -Headers $headers -SkipCertificateCheck
}

function Write-JsonFile {
    param([Parameter(Mandatory)]$Data, [Parameter(Mandatory)][string]$RelPath)
    $full = Join-Path $DataRoot $RelPath
    $dir = Split-Path -Parent $full
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    ($Data | ConvertTo-Json -Depth 20) | Set-Content -Path $full -Encoding utf8NoBOM
    Write-Host "  skrev $RelPath" -ForegroundColor Green
}

function Nu {
    [DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
}

Write-Host "Eksporterer fra $BaseUrl ..." -ForegroundColor Cyan

# ---------------------------------------------------------------------------------------------
# 1. Katalogfiler — hele resultatet, ingen filtrering
# ---------------------------------------------------------------------------------------------
Write-Host "`n[1/5] Rettskilder-katalog" -ForegroundColor Cyan
$rettskilder = Get-Api "/api/rettskilder"
Write-JsonFile -RelPath "rettskilder/katalog.json" -Data ([ordered]@{
    "_kilde"    = "GET /api/rettskilder (hele resultatet, ingen filtrering) — eksportert $(Nu)"
    "_merk"     = "Katalogfil: kun metadata (id, tittel, kildetype, departement, eier). Ingen fulltekst."
    "antall"    = $rettskilder.Count
    "rettskilder" = $rettskilder
})

Write-Host "`n[2/5] Virksomheter-katalog" -ForegroundColor Cyan
$virksomheter = Get-Api "/api/virksomheter"
Write-JsonFile -RelPath "virksomheter/katalog.json" -Data ([ordered]@{
    "_kilde"        = "GET /api/virksomheter (hele resultatet, ingen filtrering) — eksportert $(Nu)"
    "antall"        = $virksomheter.Count
    "virksomheter"  = $virksomheter
})

# ---------------------------------------------------------------------------------------------
# 2. Virksomhets-detaljfiler
# ---------------------------------------------------------------------------------------------
Write-Host "`n[3/5] Virksomhets-detaljfiler ($($VirksomhetIderForDetalj.Count) stk)" -ForegroundColor Cyan
foreach ($id in $VirksomhetIderForDetalj) {
    $v = $virksomheter | Where-Object { $_.id -eq $id }
    if (-not $v) {
        Write-Warning "Fant ikke virksomhet $id i katalogen — hopper over detaljfil."
        continue
    }
    $whereUsed = Get-Api "/api/virksomheter/$id/where-used"
    $tildelinger = Get-Api "/api/virksomheter/$id/myndighetstildelinger"
    $navneformer = Get-Api "/api/virksomheter/$id/begrep"
    Write-JsonFile -RelPath "virksomheter/$id.json" -Data ([ordered]@{
        "_kilde"                  = "GET /api/virksomheter/{id}/where-used + /myndighetstildelinger + /begrep for id=$id — eksportert $(Nu)"
        "id"                      = $id
        "navn"                    = $v.navn
        "visningsnavn"            = $v.visningsnavn
        "organisasjonsnummer"     = $v.organisasjonsnummer
        "forvaltningsniva"        = $v.forvaltningsniva
        "whereUsed"               = $whereUsed
        "myndighetstildelinger"   = $tildelinger
        "navneformer"             = $navneformer
    })
}

# ---------------------------------------------------------------------------------------------
# 3. Gruppe-register + detaljfiler
# ---------------------------------------------------------------------------------------------
Write-Host "`n[4/5] Gruppebegrep-register + detaljfiler ($($GruppeIderForDetalj.Count) stk)" -ForegroundColor Cyan
$alleGrupper = Get-Api "/api/gruppebegrep"
Write-JsonFile -RelPath "grupper/register.json" -Data ([ordered]@{
    "_kilde"        = "GET /api/gruppebegrep (hele resultatet) — eksportert $(Nu)"
    "_merk"         = "Støtteregister for navnoppslag på grupper-/koblinger-sidene (id -> term/hjemmel)."
    "antall"        = $alleGrupper.Count
    "gruppebegrep"  = $alleGrupper
})

foreach ($id in $GruppeIderForDetalj) {
    $g = $alleGrupper | Where-Object { $_.id -eq $id }
    if (-not $g) {
        Write-Warning "Fant ikke gruppebegrep $id i registeret — hopper over detaljfil."
        continue
    }
    $medlemsgrupper = Get-Api "/api/gruppebegrep/$id/medlemsgrupper"
    $overordnede = Get-Api "/api/gruppebegrep/$id/overordnede-grupper"
    $tildelinger = Get-Api "/api/gruppebegrep/$id/tildelinger"
    Write-JsonFile -RelPath "grupper/$id.json" -Data ([ordered]@{
        "_kilde"            = "GET /api/gruppebegrep/{id}/medlemsgrupper + /overordnede-grupper + /tildelinger for id=$id — eksportert $(Nu)"
        "id"                = $id
        "term"              = $g.term
        "status"            = $g.status
        "lovkildeId"        = $g.lovkildeId
        "lovreferanseEid"   = $g.lovreferanseEid
        "medlemsgrupper"    = $medlemsgrupper
        "overordnedeGrupper" = $overordnede
        "tildelinger"       = $tildelinger
    })
}

# ---------------------------------------------------------------------------------------------
# 4. Begrep-oversikt + detaljfiler (krever X-Bruker-Id)
# ---------------------------------------------------------------------------------------------
Write-Host "`n[5/5] Begreper-oversikt + detaljfiler ($($BegrepIderForDetalj.Count) stk)" -ForegroundColor Cyan
$alleBegreper = Get-Api "/api/begreper" -MedBrukerHeader
$skosBegreper = $alleBegreper | Where-Object { -not $_.begrepskategori -or $_.begrepskategori -eq "gruppe" }
Write-JsonFile -RelPath "begreper/oversikt.json" -Data ([ordered]@{
    "_kilde"                  = "GET /api/begreper (filtrert til fakta-/handlingsbegrep + gruppebegrep) — eksportert $(Nu)"
    "_merk"                   = "Virksomhets-navneformer (begrepskategori='virksomhet') er UTELATT her — de hører til virksomhetenes egne sider, ikke begrepsregisteret."
    "antall"                  = $skosBegreper.Count
    "antallTotaltIRegisteret" = $alleBegreper.Count
    "begreper"                = $skosBegreper
})

foreach ($id in $BegrepIderForDetalj) {
    $detalj = Get-Api "/api/begreper/$id"
    $bruktI = Get-Api "/api/begreper/$id/brukt-i-rettskilder"
    $merged = [ordered]@{}
    $detalj.PSObject.Properties | ForEach-Object { $merged[$_.Name] = $_.Value }
    $merged["_kilde"] = "GET /api/begreper/{id} + /brukt-i-rettskilder for id=$id — eksportert $(Nu)"
    $merged["bruktIRettskilder"] = $bruktI
    Write-JsonFile -RelPath "begreper/$id.json" -Data $merged
}

Write-Host "`nFerdig. Alle filer skrevet under $DataRoot" -ForegroundColor Cyan
Write-Host "Husk: sjekk gjennom nettside/data/README.md sin personvern-seksjon etter en eksport av NYE id-er." -ForegroundColor Yellow
