#!/usr/bin/env bash
# Sécurisation du VPS contre les attaques sur SSH (des centaines de milliers de tentatives par mois, toutes de robots).
# Lancé par installer-serveur.sh ; relançable sans risque, à refaire après l'ajout de la clé de GitHub (README, « CI/CD ») :
#   bash ~/monsuta-potaru/deploy/securiser-serveur.sh
# 1. fail2ban : une adresse qui rate 5 fois en 10 minutes est bannie 1 h, puis plus longtemps à chaque récidive.
# 2. SSH resserré : seul l'utilisateur courant se connecte, 3 essais, 1 min pour s'identifier (phrase de passe de la clé
#    comprise : le délai court pendant qu'elle est demandée), pas de X11.
# 3. Clé de GitHub Actions bridée (« restrict ») : ni redirection, ni agent, ni terminal ; scp et commandes marchent.
set -euo pipefail
UTILISATEUR="$(id -un)"
# Lancé avec sudo, AllowUsers ne laisserait entrer que root, lui-même interdit : plus personne ne pourrait se connecter.
if [[ "$UTILISATEUR" == root ]]; then
    echo "À lancer sans sudo, avec l'utilisateur qui se connecte en SSH (le script demande sudo lui-même)."
    exit 1
fi

echo "== 1/3 fail2ban"
sudo apt-get update -qq
# python3-systemd : Debian 13 n'écrit plus /var/log/auth.log, fail2ban lit le journal de systemd.
sudo DEBIAN_FRONTEND=noninteractive apt-get install -y -qq fail2ban python3-systemd > /dev/null
sudo tee /etc/fail2ban/jail.d/monsuta-potaru.local > /dev/null <<'EOF'
# Écrit par deploy/securiser-serveur.sh (Monsuta Potaru) : relancer le script plutôt que modifier ce fichier.
[DEFAULT]
bantime = 1h
findtime = 10m
maxretry = 5
# Récidive : 1 h, 2 h, 4 h... jusqu'à une semaine.
bantime.increment = true
bantime.maxtime = 1w

[sshd]
enabled = true
backend = systemd
# Compte aussi les robots qui coupent avant de s'identifier.
mode = aggressive
EOF
# Depuis OpenSSH 9.8 (Debian 13), les messages viennent de « sshd-session » : le filtre livré n'attend que « sshd » et ne
# reconnaissait aucune tentative (vérifié le 2026-11-26 : 0 échec compté pour 10 dans le journal en 5 minutes).
sudo tee /etc/fail2ban/filter.d/sshd.local > /dev/null <<'EOF'
# Écrit par deploy/securiser-serveur.sh (Monsuta Potaru) : relancer le script plutôt que modifier ce fichier.
[DEFAULT]
_daemon = sshd(?:-session)?
EOF
sudo systemctl enable --now fail2ban > /dev/null 2>&1
sudo systemctl restart fail2ban
for _ in $(seq 15); do
    sudo fail2ban-client status sshd > /dev/null 2>&1 && break
    sleep 1
done
sudo fail2ban-client status sshd | grep -E "Currently|Total"
# Le filtre essayé sur la dernière demi-heure du journal : des tentatives écrites mais aucune reconnue = fail2ban aveugle.
journal=$(mktemp)
sudo journalctl -u ssh --since "-30 min" -q -o short --no-pager > "$journal"
tentatives=$(grep -cE "Invalid user|Failed |preauth" "$journal" || true)
reconnues=$(sudo fail2ban-regex "$journal" "sshd[mode=aggressive]" 2> /dev/null | sed -nE 's/^Lines: .* ([0-9]+) matched.*/\1/p')
rm -f "$journal"
echo "  Dernière demi-heure : $tentatives tentatives dans le journal, ${reconnues:-?} reconnues par le filtre."
if [[ "$tentatives" -gt 0 && "${reconnues:-0}" -eq 0 ]]; then
    echo "  ATTENTION : fail2ban ne reconnaît aucune tentative, il ne bannira personne."
fi

echo "== 2/3 SSH"
# sshd garde la première valeur lue : 10-* passe après un 00-* déjà présent ; les valeurs finales sont vérifiées plus bas.
sudo tee /etc/ssh/sshd_config.d/10-monsuta-potaru.conf > /dev/null <<EOF
# Écrit par deploy/securiser-serveur.sh (Monsuta Potaru) : relancer le script plutôt que modifier ce fichier.
AllowUsers $UTILISATEUR
PermitRootLogin no
PasswordAuthentication no
KbdInteractiveAuthentication no
MaxAuthTries 3
LoginGraceTime 60
X11Forwarding no
EOF
# Réglage refusé : fichier retiré, SSH n'est pas rechargé (la connexion en cours et les suivantes restent possibles).
if ! sudo sshd -t; then
    sudo rm -f /etc/ssh/sshd_config.d/10-monsuta-potaru.conf
    echo "Réglages de SSH refusés par sshd -t : rien n'a changé pour SSH."
    exit 1
fi
sudo systemctl reload ssh
reglages=$(sudo sshd -T)
attendu="allowusers $UTILISATEUR|permitrootlogin no|passwordauthentication no|kbdinteractiveauthentication no|maxauthtries 3|logingracetime 60|x11forwarding no"
IFS='|' read -ra lignes <<< "$attendu"
for ligne in "${lignes[@]}"; do
    if grep -qx "$ligne" <<< "$reglages"; then
        echo "  ok : $ligne"
    else
        echo "  ATTENTION : « $ligne » attendu, mais sshd applique « $(grep "^${ligne%% *} " <<< "$reglages" || echo rien) »" \
            "(valeur fixée par un autre fichier de /etc/ssh/sshd_config.d ?)"
    fi
done

echo "== 3/3 Clé de GitHub Actions"
cles="$HOME/.ssh/authorized_keys"
if ! grep -qE ' github-actions-[^ ]*$' "$cles" 2> /dev/null; then
    echo "  Pas de clé « github-actions-... » dans $cles : à relancer après l'avoir ajoutée (README, « CI/CD »)."
elif grep -E ' github-actions-[^ ]*$' "$cles" | grep -qvE '^(ssh-|ecdsa-|sk-)'; then
    echo "  Déjà bridée (options présentes) : rien à faire."
else
    cp -p "$cles" "$cles.avant-securisation"
    # Les lignes de GitHub qui commencent par le type de clé reçoivent « restrict » devant ; les autres ne bougent pas.
    sed -i -E '/ github-actions-[^ ]*$/ s/^(ssh-|ecdsa-|sk-)/restrict \1/' "$cles"
    echo "  Bridée (copie de l'ancien fichier : $cles.avant-securisation)."
fi
grep -E ' github-actions-[^ ]*$' "$cles" 2> /dev/null | awk '{print "  " $1, $2, "...", $NF}' || true

echo
echo "Sécurisation terminée. Garde cette connexion ouverte et vérifie dans un autre terminal que « ssh » marche encore."
