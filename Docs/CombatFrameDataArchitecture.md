# Combat v0.7 — architecture Frame Data déterministe

## But

Le résultat du combat est désormais calculé sur une horloge logique fixe de
60 ticks par seconde. Le framerate graphique, les coroutines et les Animator
Events ne décident plus d'un impact, d'une invulnérabilité ou d'une durée de
stun.

Le système conserve les contrôleurs existants comme façades publiques :

- `FighterCombat` reçoit les commandes du Gesture Pad et expose les états au
  HUD ;
- `CombatSpatialController` reste l'unique autorité des positions neutres,
  distances et orientations ;
- `CombatFrameSystem` orchestre les deux combattants et résout les impacts ;
- `EnemyAutoCombat` soumet des commandes au même système que le joueur.

## Ordre d'un tick

1. `CombatFrameClock` émet un tick entier et contigu.
2. Les deux `CombatActionRunner` mettent à jour buffer, stun et phase locale.
3. L'autorité spatiale avance les marches et le timer de flanc.
4. Les deux candidats d'impact sont collectés.
5. `CombatHitResolver` classe le groupe complet.
6. Les résultats sont appliqués symétriquement.
7. Les frames locales non gelées avancent.
8. La télémétrie devient observable.

Cette collecte avant application rend un Trade indépendant de l'ordre des
GameObjects ou des abonnements Unity.

## Profils initiaux

| Action | Startup | Active | Recovery | Total | Hitstop | Hitstun | Blockstun |
|---|---:|---:|---:|---:|---:|---:|---:|
| A | 7 | 3 | 12 | 22 | 3 | 16 | 9 |
| B | 11 | 4 | 17 | 32 | 4 | 22 | 13 |
| C | 18 | 5 | 26 | 49 | 6 | 34 | 18 |

Les dégâts, coûts et portées suivent la hiérarchie `A < B < C`. Chaque action
est résolue en un snapshot immuable au démarrage. Le point d'extension
`ICombatActionModifier` permet d'ajouter plus tard race, équipement, artefacts,
talents et buffs dans un ordre stable, sans réécrire le runner.

## Résolution d'un impact

Une attaque démarre sans exiger que la cible soit devant l'attaquant. Pendant
chaque frame active, le resolver vérifie la position réelle :

- portée propre à l'action ;
- cône horizontal de 100 degrés dans la direction réelle de l'attaquant ;
- invulnérabilité et fenêtre parfaite ;
- parade ou garde, uniquement depuis Face ;
- phase de la cible pour distinguer Hit, Counter Hit et Punish.

Une même attaque ne peut résoudre qu'une fois contre sa cible. Si aucune frame
active ne touche, elle se termine en Whiff. Deux impacts valides pendant le
même tick deviennent un Trade et sont appliqués tous les deux.

## Hitstop, Hitstun et Blockstun

Le compteur global continue pendant le Hitstop, mais les frames locales des
combattants concernés sont gelées. Après le gel, l'attaquant reprend son
Recovery et la cible poursuit son Hitstun ou son Blockstun.

Un Hitstun interrompt l'action en cours et vide le buffer de la victime. Un
Blockstun coûte 15 endurance uniquement lorsqu'un impact a réellement été
bloqué. La garde maintenue reprend après le Blockstun si le maintien est
toujours demandé.

La garde brisée n'est déclenchée que si cet impact bloqué amène l'endurance à
zéro. Elle dure 240 ticks et restaure progressivement exactement 15 endurance.
Une esquive, une permutation ou une autre dépense atteignant zéro ne déclenche
pas de garde brisée.

## Parade et riposte

La parade active dure 7 frames, conversion du timing existant d'environ
0,12 seconde. Une parade réussie termine immédiatement la défense et ouvre une
fenêtre de riposte manuelle de 30 frames. Aucune attaque, aucun bonus de dégâts
et aucun coup garanti ne sont créés automatiquement.

## Esquives

Toutes les esquives partagent initialement le profil suivant :

- durée totale : 26 frames ;
- démarrage vulnérable : `[0, 5)` ;
- invulnérabilité : `[5, 19)` ;
- esquive parfaite : `[9, 15)` ;
- récupération vulnérable : `[19, 26)`.

Le déplacement commence dès le Startup. La destination spatiale est validée à
l'entrée de la frame 19. Un coup reçu avant les i-frames interrompt l'esquive :
la position horizontale réelle au moment de l'impact devient la nouvelle pose
neutre. Il n'y a ni rollback, ni snap vers l'ancre précédente. Une attaque
rencontrant les i-frames est consommée comme Dodge ou Perfect Dodge et
l'esquive continue.

## Permutation, recharge et orientation

La permutation possède 3 frames de Startup, une invulnérabilité `[2, 8)` et
6 frames de Recovery. Son coût reste 50, elle fonctionne avec exactement 50,
et conserve l'orientation actuelle.

La recharge H utilise un Startup et des gains périodiques en ticks entiers.
Il n'existe toujours aucune régénération passive.

Le timer de flanc dure 180 frames. À son terme, la remise Face est appliquée à
la première frame sûre ; elle ne coupe pas une action en cours. Le dos ne lance
aucun timer automatique.

## Buffer de commandes

Chaque combattant possède un buffer d'un seul emplacement et de 6 frames :

- une nouvelle commande remplace l'ancienne ;
- aucune file A → B → C n'est construite ;
- le buffer expire de façon déterministe ;
- Hitstun, garde brisée, mort, interruption majeure et Rejouer le nettoient.

## Reset et interruption

`CombatFrameSystem.ResetSystem` remet à zéro l'horloge, les deux runners, les
buffers, les fenêtres, les stuns et les transactions spatiales. Le reset est
idempotent. `EnemyAutoCombat.StopAI` désabonne son unique tick, annule ses
décisions différées et empêche la duplication de boucle après Rejouer.

## Éléments restés graphiques

Les fades, pulsations, textes temporaires, caméra et autres effets purement
visuels utilisent encore le temps Unity. Ils n'ont aucune autorité sur les
dégâts ou les fenêtres de combat.

## Validation

- `V07FrameDataValidation` vérifie les profils, avantages calculés, fenêtres,
  buffer et horloge.
- `V07PlayModeValidation` lance réellement `CombatArena` et vérifie attaques,
  Hitstop, Whiff hors axe, Trade, expiration et nettoyage du buffer, parade,
  garde, garde brisée, esquive interrompue sans rollback, i-frames, esquive
  parfaite, permutation et son invulnérabilité, timer de flanc, endurance
  illimitée et reset.

Les valeurs restent des paramètres sérialisés de `CombatFrameDataSettings` afin
de pouvoir être ajustées sans disperser les règles dans plusieurs scripts.
