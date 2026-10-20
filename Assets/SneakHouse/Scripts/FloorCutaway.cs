using System.Collections.Generic;
using UnityEngine;

namespace SneakHouse
{
    [DefaultExecutionOrder(100)]
    public class FloorCutaway : MonoBehaviour
    {
        public int FloorNumber { get; private set; } = 1;
        public bool UpperVisible { get; private set; }
        private HouseShell shell;
        private GameSession game;
        private ChaseCamera cameraView;
        private HouseShellVariant cachedShell;
        private RoomLayoutVariant cachedRoom;
        private readonly List<Renderer> upperArt = new List<Renderer>();
        private readonly Dictionary<GuardBrain,Renderer[]> guards = new Dictionary<GuardBrain,Renderer[]>();
        private readonly Dictionary<Renderer,bool> originals = new Dictionary<Renderer,bool>();
        private void Start()
        {
            game = GetComponent<GameSession>(); shell = GetComponent<HouseShell>();
            cameraView = Camera.main.GetComponent<ChaseCamera>();
            Refresh();
        }
        private void Cache()
        {
            Restore(); upperArt.Clear(); guards.Clear(); originals.Clear();
            cachedShell = shell.Current; cachedRoom = GetComponent<InteriorLayout>().CurrentRooms[1];
            upperArt.AddRange(shell.Current.upperArchitecture.GetComponentsInChildren<Renderer>(true));
            upperArt.AddRange(cachedRoom.GetComponentsInChildren<Renderer>(true));
            foreach (Renderer renderer in upperArt) originals[renderer] = renderer.forceRenderingOff;
            foreach (GuardBrain guard in game.guards)
            {
                guards[guard] = guard.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer renderer in guards[guard]) originals[renderer] = renderer.forceRenderingOff;
            }
        }
        private void LateUpdate() { Refresh(); }
        public void Refresh()
        {
            if (shell == null || !shell.HasUpperFloor || game == null) { Restore(); return; }
            if (cachedShell != shell.Current || cachedRoom != GetComponent<InteriorLayout>().CurrentRooms[1]) Cache();
            float height = shell.Current.upperHeight;
            if (game.player.transform.position.y > height-.4f) FloorNumber = 2;
            else if (game.player.transform.position.y < height-.9f) FloorNumber = 1;
            // Perspective must retain real architecture; overhead removes the floor obscuring play below.
            bool perspective = cameraView != null && cameraView.ViewBlend > .2f;
            UpperVisible = FloorNumber == 2 || perspective;
            foreach (Renderer renderer in upperArt) Set(renderer,!UpperVisible);
            foreach (var entry in guards)
                foreach (Renderer renderer in entry.Value) Set(renderer,!perspective && FloorNumber == 1 && entry.Key.transform.position.y > height-.5f);
        }
        private void Set(Renderer renderer, bool hidden)
        {
            if (renderer != null) renderer.forceRenderingOff = hidden || originals[renderer];
        }
        private void Restore()
        {
            foreach (var entry in originals) if (entry.Key != null) entry.Key.forceRenderingOff = entry.Value;
            FloorNumber = 1; UpperVisible = false;
        }
        private void OnDisable() { Restore(); }
    }
}
