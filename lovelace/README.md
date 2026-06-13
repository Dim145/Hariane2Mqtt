# Hariane Water Card

Carte Lovelace pour Home Assistant affichant la consommation d'eau récupérée par Hariane2Mqtt,
dans l'esprit de [content-card-linky](https://github.com/MyElectricalData/content-card-linky) et
[lovelace-gazpar-card](https://github.com/ssenart/lovelace-gazpar-card) :

- **Dernier relevé** (hero) + total compteur ;
- tuiles **Semaine / Mois / Année** avec tendance (% vs période précédente) ;
- **histogramme** des derniers jours de consommation.

La carte lit la **statistique long-terme** `hariane:water_<contrat>` importée par Hariane2Mqtt
(`recorder/statistics_during_period`) — l'option **`import_energy_statistics` doit donc être activée**.
Aucune dépendance externe, aucune étape de build : c'est un simple fichier `.js`.

## Installation via HACS

Ce dépôt expose la carte comme plugin HACS ([`hacs.json`](../hacs.json)) et chaque release y attache `hariane-water-card.js`.

1. HACS → ⋮ → **Dépôts personnalisés** → ajoutez `https://github.com/Dim145/Hariane2Mqtt`, catégorie **Tableau de bord**.
2. Installez « Hariane Water Card », puis ajoutez la carte à un tableau de bord (un éditeur graphique est disponible).

## Installation manuelle

1. Copiez [`hariane-water-card.js`](hariane-water-card.js) dans `config/www/` de Home Assistant
   (→ accessible sous `/local/hariane-water-card.js`).
2. Déclarez la ressource : **Paramètres → Tableaux de bord → ⋮ → Ressources → Ajouter** :
   - URL : `/local/hariane-water-card.js`
   - Type : **Module JavaScript**
   (ou en YAML : `lovelace: resources: [{ url: /local/hariane-water-card.js, type: module }]`)
3. Ajoutez la carte à un tableau de bord :

```yaml
type: custom:hariane-water-card
statistic_id: hariane:water_123456789   # requis (votre n° de contrat)
title: Eau
days: 14
price_per_m3: 4.30                       # optionnel : affiche un coût estimé
```

## Options

| Option         | Type   | Défaut | Description                                                        |
|----------------|--------|--------|--------------------------------------------------------------------|
| `statistic_id` | string | —      | **Requis.** Id de la statistique, ex. `hariane:water_123456789`.   |
| `title`        | string | `Eau`  | Titre affiché dans l'en-tête.                                      |
| `days`         | number | `14`   | Nombre de jours dans l'histogramme.                                |
| `unit`         | string | `m³`   | Unité affichée.                                                    |
| `price_per_m3` | number | —      | Si défini, affiche le coût estimé du mois.                         |
| `show_header`  | bool   | `true` | Affiche l'en-tête (titre + icône).                                 |
| `show_icon`    | bool   | `true` | Affiche l'icône goutte.                                            |
| `color`        | string | —      | Couleur d'accent (sinon dégradé eau par défaut, suit le thème HA). |

## Aperçu sans Home Assistant

Un [`preview.html`](preview.html) monte la carte avec des données d'exemple (thème clair + sombre).
Servez le dossier puis ouvrez-le :

```bash
python3 -m http.server 8731 --directory lovelace
# puis http://localhost:8731/preview.html
```
