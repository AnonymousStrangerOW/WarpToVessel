using OWML.Common;
using OWML.ModHelper;
using UnityEngine;
using System.Collections;
using NewHorizons.Utility;
using HarmonyLib;
using System.Reflection;
using System.IO;
using UnityEngine.UI;
using static StencilPreviewImageEffect;

namespace WarpToVessel
{
    public class WarpToVessel : ModBehaviour
    {
        // global variables
        protected PlayerSpawner _spawner; // for spawning the player
        public SpawnPoint vessel; // to store the spawn point at the vessel
        public const float blinkTime = 0.5f; // constant for blink time
        public const float animTime = blinkTime / 2f; // constant for blink animation time
        private SubmitAction warpButton;
        private bool isInShip; // boolean value to store if the player entered the ship or not
        public static WarpToVessel Instance; // to store the instance of the mod
        public static INewHorizons newHorizons { get; private set; } // to store nh api

        // enables harmony patch
        private void Awake()
        {
            Instance = this;
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
        }

        private void Start()
        {
            // Get the New Horizons API and load configs
            newHorizons = ModHelper.Interaction.TryGetModApi<INewHorizons>("xen.NewHorizons");
            newHorizons.LoadConfigs(this);

            // Example of accessing game code.
            LoadManager.OnCompleteSceneLoad += (scene, loadScene) =>
            {
                if (loadScene != OWScene.SolarSystem) return;
            };
        }

        private void Update()
        {
            if (warpButton != null)
            {
                if (Check())
                {
                    warpButton.gameObject.SetActive(true);
                    
                    if (Locator.GetPlayerSectorDetector().IsWithinSector(Sector.Name.Ship))
                    {
                        warpButton.enabled = false;
                        isInShip = true;
                    } else
                    {
                        warpButton.enabled = true;
                        isInShip = false;
                    }
                    warpButton.gameObject.GetComponent<UIStyleApplier>().ChangeState(isInShip ? UIElementState.DISABLED : UIElementState.NORMAL, false);
                }
                else
                {
                    warpButton.gameObject.SetActive(false);
                }
            }
        }

        // blink coroutine method
        private IEnumerator Blink()
        {
            var cameraEffectController = FindObjectOfType<PlayerCameraEffectController>(); // gets camera controller

            // close eyes
            cameraEffectController.CloseEyes(animTime); // closes eyes
            yield return new WaitForSeconds(animTime);  // waits until animation stops to proceed to next line
            GlobalMessenger.FireEvent("PlayerBlink"); // fires an event for the player blinking

            // warp core stuff
            OWItem item = SearchUtilities.Find("TowerTwin_Body/Sector_TowerTwin/Sector_TimeLoopInterior/Interactables_TimeLoopInterior/WarpCoreSocket/Prefab_NOM_WarpCoreVessel").GetComponent<OWItem>(); // get atp's advanced warp core
            var controller = GameObject.FindObjectOfType<TimeLoopCoreController>(); // gets atp core controller
            controller.OpenCore(); // makes controller think the core is open
            controller.OnSocketableRemoved(item); // tells controller you removed the item
            Locator.GetToolModeSwapper().GetItemCarryTool().PickUpItemInstantly(item); // gives player the warp core

            // gives player suit if doesn't have one.
            if (!Locator.GetPlayerSuit().IsWearingSuit())
            {
                Locator.GetPlayerSuit().SuitUp(false, true, true); // equip suit instantly
            }

            // vessel spawn stuff
            vessel = SearchUtilities.Find("DB_VesselDimension_Body/Sector_VesselDimension/Spawn_Vessel").GetComponent<SpawnPoint>(); // gets vessel spawn point
            _spawner = GameObject.FindGameObjectWithTag("Player").GetRequiredComponent<PlayerSpawner>(); // gets player spawner
            _spawner.DebugWarp(vessel); // warps you to vessel
            

            // open eyes
            cameraEffectController.OpenEyes(animTime, false); // open eyes
            yield return new WaitForSeconds(animTime); //  waits until animation stops to proceed to next line
        }
        
        private bool Check()
        {
            return Locator.GetShipLogManager().IsFactRevealed("GABBRONEW_E"); // checks if the player talked to gabbro about the new trick.
        }

        // adds button to pause and its function
        public override void SetupPauseMenu(IPauseMenuManager pauseMenu)
        {
            // method to delay it to start after system is loaded
            WarpToVessel.Instance.ModHelper.Events.Unity.FireInNUpdates(() =>
            {
                // checks if in solar system
                if (LoadManager.GetCurrentScene() == OWScene.SolarSystem && Check())
                {
                    base.SetupPauseMenu(pauseMenu); // sets up pause menu
                    warpButton = pauseMenu.MakeSimpleButton("MEDITATE TO VESSEL", 3, true); // adds button to pause menu
                    warpButton.OnSubmitAction += () =>
                    {
                        StartCoroutine(Blink()); // calls method to make player blink
                    };
                }
            }, 5);
        }

        // harmony patch for gabbro dialogue
        [HarmonyPatch]
        public class Patch
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(GabbroDialogueSwapper), nameof(GabbroDialogueSwapper.Start))]
            public static void GabbroDialogueSwapper_Postfix()
            {
                if (TimeLoop.GetLoopCount() > 2)
                {
                    WarpToVessel.newHorizons.CreateDialogueFromXML(null, File.ReadAllText(Path.Combine(WarpToVessel.Instance.ModHelper.Manifest.ModFolderPath, "planets/dialogue/gabbro.xml")), "{ pathToExistingDialogue: \"Sector_GabbroIsland/Interactables_GabbroIsland/Traveller_HEA_Gabbro/ConversationZone_Gabbro\" }", SearchUtilities.Find("GabbroIsland_Body"));
                }
            }
        }
    }
}
