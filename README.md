[![View SBOM](https://img.shields.io/badge/sbom.sh-viewSBOM-blue?link=https%3A%2F%2Fsbom.sh%2F6a86e3bc-7f57-4b6b-878f-260ebe0b25ac)](https://sbom.sh/6a86e3bc-7f57-4b6b-878f-260ebe0b25ac)
[![Docker](https://badgen.net/badge/icon/docker?icon=docker&label)](https://hub.docker.com/repository/docker/dim145/hariane2mqtt/general)

<p align="center">
    <img src="./addon/logo.png"  height="100" title="Logo" alt="Logo introuvable" />
</p>

# Introduction

Ce programme récupère les données de consommation d'eaux fournies par le site https://www.hariane.fr/ puis les publie sur un broker MQTT.  
Beaucoup de requêtes API peuvent être exécutées, il faut donc vérifier à ne pas faire une récupération trop régulière même si aucune limite d'utilisation ne semble présente.

Attention, le projet n'est testé qu'avec l'architecture amd64. Il faut encore le faire en arm64 et autres plateformes

# Données récupérées
Les données récupérées et calculées sont les suivantes :
- la dernière valeur de consommation en date sur le site. (last_value)
- La date à laquelle correspond cette valeur (last_value_date)
- Le total de consommation existant sur hariane (option)

Attention, pour récupérer le total, l'api va récupérer toutes les données de consommation jusqu'au début des données existant sur Hariane par lot de 17 jours. Cela peut
représenté énormément d'appel api qui peut prendre du temps. Cela ne sera fait que la première fois. Une fois le total connu, le calcul se fera à partir de ce qui est connu + les nouvelles valeurs.

# Roadmap
- Ajout de l'historique des 17 derniers jours dans les attributs du capteur "last_value".
- Ajout d'une carte custom pour le tableau de bord.
