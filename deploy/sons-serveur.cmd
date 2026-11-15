@echo off
rem Lance sons-serveur.ps1 sans modifier la strategie d'execution des scripts de Windows (autorisation pour cette commande seulement).
rem Usage : deploy\sons-serveur.cmd debian@IP_DU_VPS
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0sons-serveur.ps1" %*
