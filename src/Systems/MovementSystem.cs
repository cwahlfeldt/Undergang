using System.Linq;
using System.Threading.Tasks;
using Game.Components;
using Godot;

namespace Game
{
    public class MovementSystem : System
    {
        public override async Task Update()
        {
            var moveEntites = Entities.Query<Movement>().ToList();

            if (!moveEntites.Any())
                return;

            foreach (var mover in moveEntites)
            {
                if (mover.Has<CurrentTurn>())
                {
                var (from, to) = mover.Get<Movement>();
                var path = PathFinder.FindPath(from, to, mover.Get<MoveRange>());
                var locations = path.Select(HexGrid.HexToWorld).ToList();

                await Tweener.MoveThrough(mover.Get<Instance>().Node, locations);

                var fromTile = Entities.GetAt(path.First());
                var toTile = Entities.GetAt(path.Last());

                mover.Update(new Coordinate(path.Last()));
                mover.Remove<Movement>();


                    Events.OnMoveCompleted(mover, fromTile.Get<Coordinate>(), toTile.Get<Coordinate>());
                    Events.UnitActionComplete(mover);
                }

            }
        }
    }
}
