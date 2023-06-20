using Godot;
using System;

public partial class Camera : Camera3D
{
	private Vector2 _rotation;

	public override void _Ready()
	{
	}
	
	public override void _Process(double delta)
	{
		var input = new Vector3();
		
		if (Input.IsActionPressed("move_forward"))
		{
			input.Z -= 1;
		}
		if (Input.IsActionPressed("move_backward"))
		{
			input.Z += 1;
		}
		if (Input.IsActionPressed("move_left"))
		{
			input.X -= 1;
		}
		if (Input.IsActionPressed("move_right"))
		{
			input.X += 1;
		}

		Vector3 velocity = 15 * (float)delta * input;
		Translate(velocity);

		var transform = Transform;
		transform.Basis = Basis.FromEuler(new Vector3(_rotation.Y, _rotation.X, 0));
		transform.Origin.Y = 1.038f;
		Transform = transform;
	}
	
	public override void _Input(InputEvent input)
	{
		if (input is InputEventMouseMotion && Input.IsMouseButtonPressed(MouseButton.Left))
		{
			_rotation += (-0.001f) * (input as InputEventMouseMotion).Relative;
			_rotation.X = _rotation.X % (2 * Mathf.Pi);
			_rotation.Y = _rotation.Y % (2 * Mathf.Pi);
		}
	}
}
