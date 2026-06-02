using UnityEngine;

namespace ClashUp.Client.Gameplay
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        private IMovementInput _input;
        private CharacterController _controller;
        private float _speed;

        public void Initialize(IMovementInput input, float speed = 5f)
        {
            _input = input;
            _speed = speed;
            _controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            if (_input == null) return;

            var dir = _input.Value;
            if (dir.sqrMagnitude < 0.001f) return;

            var move = new Vector3(dir.x, 0f, dir.y) * (_speed * Time.deltaTime);
            _controller.Move(move);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y)),
                10f * Time.deltaTime);
        }
    }
}
