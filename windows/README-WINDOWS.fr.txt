SpaceWall Ships pour Windows  (v0.1.0, EXPERIMENTAL : testé sur un seul PC)
===========================================================================
Fond d'écran animé qui réagit au son : nébuleuse, pluie et deux vaisseaux qui suivent votre musique. Aucune installation, aucune autre application nécessaire.
Prérequis : Windows 10/11 64 bits avec le Edge WebView2 Runtime (déjà présent sur un Windows 10/11 normal).

CE QUI A ÉTÉ TESTÉ
  Testé sur UN SEUL PC : Windows 10 Pro 22H2, Intel HD Graphics 620, audio Realtek, un écran 1920x1080.
  Là, il s'est placé derrière les icônes du bureau, a tourné à 30 images par seconde, a entendu les haut-parleurs, et les vaisseaux ont réagi au son.
  NON testé : Windows 11, d'autres cartes graphiques, plusieurs écrans, d'autres cartes son, et « Démarrer avec Windows » après un redémarrage.
  Le programme n'est pas signé : Windows vous avertit la première fois (voir plus bas).

PREMIER DÉMARRAGE
  1. Extrayez tout le zip dans un dossier (par ex. Documents\SpaceWall). Ne le lancez pas depuis l'intérieur du zip.
  2. Double-cliquez sur  1-RUN-ALL-TESTS.bat
     Si SmartScreen affiche « Windows a protégé votre ordinateur » : cliquez sur « Informations complémentaires » puis « Exécuter quand même ».
  3. Inutile de fermer vos autres fenêtres : le test les réduit puis les restaure tout seul.
  4. Attendez environ 40 secondes sans toucher la souris. Un petit bip discret sera joué. Une boîte de résultat apparaît.
  5. Le résultat est aussi dans  %APPDATA%\SpaceWall\diagnostics.txt  (copié dans le presse-papiers)
     et des captures d'écran sont dans  %APPDATA%\SpaceWall\shots\  (elles montrent votre bureau : regardez avant de partager).

APRÈS LE TEST
  - Le programme choisit tout seul le meilleur mode de placement pour votre PC et le garde.
  - Icône de la zone de notification (près de l'horloge, parfois sous la flèche ^) : clic gauche = ALLUMER/ÉTEINDRE, clic droit = menu
    (Mode A-E, Qualité, Mode démo, Affichage de débogage, Démarrer avec Windows, Diagnostic, Quitter).
  - Pour le lancer plus tard : SpaceWall-start.bat (ou cochez « Démarrer avec Windows » dans le menu de l'icône).

EN CAS DE PROBLÈME
  - Écran noir ou pas de fond d'écran : icône > Mode > « D: Fenêtre en bas » (marche toujours, mais cache les icônes du bureau)
    ou run-in-window.bat (une fenêtre normale, seulement pour vérifier l'animation et le son).
  - La fenêtre s'affiche mais ne réagit pas à la musique : icône > Diagnostic, regardez la ligne « Audio ».
  - Tout est écrit dans %APPDATA%\SpaceWall\log.txt.
  - Tout supprimer : uninstall.bat.

Code source et version Linux (KDE) : https://github.com/dghAyoub/spacewall-ships   (licence MIT ; créé avec l'aide d'un assistant IA)
