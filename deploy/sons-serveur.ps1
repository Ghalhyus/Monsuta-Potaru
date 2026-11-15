# Envoie les musiques du site (tous les .mp3 de MonsutaPotaru/wwwroot/images/sons/ : animation d'échange, combats ;
# pistes de la bande originale de HeartGold / SoulSilver) dans le dossier des images du serveur,
# ~/monsuta-potaru-images/sons. Elles ne sont ni dans Git ni téléchargeables comme les autres images (outils/images.py) :
# la musique ne se redistribue pas. À lancer une fois, et quand un fichier change ; le site les joue aussitôt, sans
# redémarrage (un fichier absent : rien n'est joué).
#   deploy\sons-serveur.cmd debian@IP_DU_VPS
param(
    [Parameter(Mandatory = $true)] [string] $Serveur
)
$ErrorActionPreference = "Stop"
$dossier = Join-Path $PSScriptRoot "..\MonsutaPotaru\wwwroot\images\sons"
$sons = @(Get-ChildItem -Path $dossier -Filter *.mp3 -ErrorAction SilentlyContinue)
if ($sons.Count -eq 0) {
    throw "Musiques absentes : $dossier. Y déposer echange.mp3, evolution.mp3 et felicitations.mp3, ou les récupérer d'un autre PC (outils\recuperer-images.cmd)."
}

Write-Host "== Envoi des $($sons.Count) musiques vers $Serveur"
ssh $Serveur "mkdir -p ~/monsuta-potaru-images/sons"
if ($LASTEXITCODE -ne 0) { throw "Connexion impossible (adresse, utilisateur ou connexion SSH ?)." }
scp $sons.FullName "${Serveur}:monsuta-potaru-images/sons/"
if ($LASTEXITCODE -ne 0) { throw "Envoi impossible (voir les messages ci-dessus)." }
Write-Host "Musiques envoyées."
