using System.Threading.Tasks;
using System.Linq;
using Game.Components;
using Godot;

namespace Game
{
    public class CombatSystem : System
    {
        private Entity _player = null;

        public override void Initialize()
        {
            _player = Entities.GetPlayer();
        }
        public override async Task Update()
        {
            var enemy = Entities.Query<Enemy, CurrentTurn>().FirstOrDefault();

            if (_player == null)
            {
                GD.Print("CombatSystem: No enemy with current turn or no player found");
                return;
            }

            if (_player.Has<WaitingForAction>())
            {
                GD.Print("CombatSystem: Player already waiting for action");
                return;
            }

            GD.Print($"CombatSystem: Setting up player action for enemy turn: {enemy.Get<Name>()}");
            _player.Add(new WaitingForAction());
            _player.Add(new MoveRange(1));
        }
    }
}
