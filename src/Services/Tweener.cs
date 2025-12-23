using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Game
{
    public partial class Tweener : Node, ISystem
    {
        public static Tweener Instance { get; private set; }
        private const float DEFAULT_MOVEMENT_DURATION = Config.NormalMoveAnimationSpeed;
        private const float DEFAULT_ROTATION_DURATION = Config.RotationAnimationSpeed;
        private const Tween.TransitionType DEFAULT_TRANS_TYPE = Tween.TransitionType.Sine;
        private const Tween.EaseType DEFAULT_EASE_TYPE = Tween.EaseType.Out;
        private readonly Dictionary<Node, Tween> _activeTweens = [];
        public override void _Ready() => Instance = this;

        public async Task MoveThrough(
            Node3D target,
            List<Vector3> positions,
            float moveDuration = DEFAULT_MOVEMENT_DURATION,
            float rotationDuration = DEFAULT_ROTATION_DURATION)
        {
            if (positions == null || positions.Count == 0 || target == null)
                return;

            StopAnimation(target);
            await MoveToNextPosition(target, new Queue<Vector3>(positions), moveDuration, rotationDuration);
        }

        private async Task MoveToNextPosition(
            Node3D target,
            Queue<Vector3> remainingPositions,
            float moveDuration,
            float rotationDuration)
        {
            if (remainingPositions.Count == 0)
            {
                return;
            }

            var nextPosition = remainingPositions.Dequeue();
            if (target.GlobalPosition.DistanceSquaredTo(nextPosition) <= 0.001f)
            {
                await MoveToNextPosition(target, remainingPositions, moveDuration, rotationDuration);
                return;
            }

            await LookAt(target, nextPosition, rotationDuration);

            var moveTween = CreateTween();
            _activeTweens[target] = moveTween;

            var tcs = new TaskCompletionSource();
            moveTween.Finished += () => tcs.SetResult();

            moveTween.TweenProperty(
                target,
                "global_position",
                nextPosition,
                moveDuration
            ).SetTrans(DEFAULT_TRANS_TYPE)
            .SetEase(DEFAULT_EASE_TYPE);

            await tcs.Task;
            await MoveToNextPosition(target, remainingPositions, moveDuration, rotationDuration);
        }

        public async Task LookAt(Node3D target, Vector3 point, float duration = DEFAULT_ROTATION_DURATION)
        {
            if (target == null)
            {
                return;
            }

            var tween = CreateTween();
            _activeTweens[target] = tween;

            var tcs = new TaskCompletionSource();
            tween.Finished += () =>
            {
                _activeTweens.Remove(target);
                tcs.SetResult();
            };

            // Use quaternion for proper rotation interpolation (SLERP)
            var targetBasis = target.GlobalTransform.LookingAt(point, Vector3.Up).Basis;
            var targetQuaternion = targetBasis.GetRotationQuaternion();

            tween.TweenProperty(
                target,
                "quaternion",
                targetQuaternion,
                duration
            ).SetTrans(DEFAULT_TRANS_TYPE)
            .SetEase(DEFAULT_EASE_TYPE);

            await tcs.Task;
        }

        public void StopAnimation(Node target)
        {
            if (_activeTweens.TryGetValue(target, out var tween))
            {
                tween.Kill();
                _activeTweens.Remove(target);
            }
        }

        public void StopAllAnimations()
        {
            foreach (var tween in _activeTweens.Values)
            {
                tween.Kill();
            }
            _activeTweens.Clear();
        }

        public bool IsAnimating(Node target) => _activeTweens.ContainsKey(target);

        /// <summary>
        /// Animates an attack by lunging toward target position and back
        /// </summary>
        public async Task AttackAnimation(
            Node3D attacker,
            Vector3 targetPosition,
            float lungeDuration = Config.AttackLungeDuration)
        {
            if (attacker == null)
                return;

            StopAnimation(attacker);

            var startPosition = attacker.GlobalPosition;

            // Look at target first
            await LookAt(attacker, targetPosition, 0.1f);

            // Lunge forward
            var lungePosition = startPosition.Lerp(targetPosition, Config.AttackLungeDistance);

            var lungeTween = CreateTween();
            _activeTweens[attacker] = lungeTween;

            var lungeTcs = new TaskCompletionSource();
            lungeTween.Finished += () => lungeTcs.SetResult();

            lungeTween.TweenProperty(
                attacker,
                "global_position",
                lungePosition,
                lungeDuration
            ).SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);

            await lungeTcs.Task;

            // Quick return back
            var returnTween = CreateTween();
            _activeTweens[attacker] = returnTween;

            var returnTcs = new TaskCompletionSource();
            returnTween.Finished += () =>
            {
                _activeTweens.Remove(attacker);
                returnTcs.SetResult();
            };

            returnTween.TweenProperty(
                attacker,
                "global_position",
                startPosition,
                lungeDuration * 0.7f  // Slightly faster return
            ).SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.In);

            await returnTcs.Task;
        }
    }
}
