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
- L'historique de consommation quotidienne dans le dashboard Énergie de Home Assistant, daté au bon jour (option, voir ci-dessous)
- L'index réel du compteur (index)
- Une alerte de fuite suspectée (binary_sensor `leak_suspected`, option `leak_detection`)
- L'horodatage du dernier relevé réussi (`last_update`) et un topic de disponibilité MQTT (Last Will : passe `offline` si le programme plante en cours d'exécution)

Attention, pour récupérer le total, l'api va récupérer toutes les données de consommation jusqu'au début des données existant sur Hariane par lot de 17 jours. Cela peut
représenté énormément d'appel api qui peut prendre du temps. Cela ne sera fait que la première fois. Une fois le total connu, le calcul se fera à partir de ce qui est connu + les nouvelles valeurs.

# Dashboard Énergie de Home Assistant

En plus de la publication MQTT, le programme peut injecter la consommation quotidienne directement dans les **statistiques long-terme** de Home Assistant, afin qu'elle apparaisse dans le **dashboard Énergie → Eau**, **datée au bon jour**. (Le dashboard Énergie ne lit pas les états MQTT, toujours enregistrés à l'instant présent : il faut donc passer par l'API statistiques de HA.)

Activez l'option `import_energy_statistics` (add-on) ou la variable `IMPORT_ENERGY_STATISTICS=true` (Docker). La donnée est exposée comme statistique externe `hariane:water_<contrat>` (unité m³, classe `volume`).

- **En add-on Home Assistant** : aucune configuration supplémentaire — l'accès à l'API se fait automatiquement via le superviseur (`SUPERVISOR_TOKEN`).
- **En Docker autonome** : renseignez `HASS_URL` (ex. `http://homeassistant.local:8123`) et `HASS_TOKEN`, un *long-lived access token* d'un utilisateur **administrateur** (l'import de statistiques nécessite les droits admin).

Ensuite, dans Home Assistant : **Paramètres → Tableaux de bord → Énergie → Consommation d'eau → Ajouter une source**, puis sélectionnez la statistique `Hariane Water <contrat>`.

> Au premier lancement (ou après migration depuis une ancienne version), tout l'historique disponible sur Hariane est récupéré et importé une seule fois (opération potentiellement longue, par lots de 17 jours). Les exécutions suivantes n'ajoutent que les nouveaux jours.

# Carte Lovelace

Une carte custom (dans l'esprit de [content-card-linky](https://github.com/MyElectricalData/content-card-linky) / [lovelace-gazpar-card](https://github.com/ssenart/lovelace-gazpar-card)) est fournie dans [`lovelace/`](lovelace/) : dernier relevé, totaux semaine/mois/année avec tendances, et histogramme des derniers jours. Elle lit la statistique `hariane:water_<contrat>` (nécessite `import_energy_statistics`). Installation et options : [lovelace/README.md](lovelace/README.md).

# Roadmap
- Ajout de l'historique des 17 derniers jours dans les attributs du capteur "last_value".
