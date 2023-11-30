using Godot;

public partial class Main : Node3D
{
	private Vector2 _oldUserPosition;
	private ModelGenerator _modelGenerator;
	private CityScheme _cityScheme;
	private CharacterBody3D _player;

	public override void _Ready()
	{
		_modelGenerator = GetNode<ModelGenerator>("%ModelGenerator");
		_cityScheme = GetNode<CityScheme>("%CityScheme");
		_player = GetNode<CharacterBody3D>("%Player");
	}

	public override void _Process(double delta)
	{
		_cityScheme.UserPosition = new Vector2(_player.Transform.Origin.X, _player.Transform.Origin.Z);
		if ((_cityScheme.UserPosition - _oldUserPosition).LengthSquared() >= 50)
		{
			_oldUserPosition = _cityScheme.UserPosition;
			_cityScheme.QueueRedraw();
		}
	}
	
	public override void _Input(InputEvent e)
	{
		SchemeState newState;
		if (e.IsActionPressed("toggle_graph"))
		{
			newState = SchemeState.Graph;
		} else if (e.IsActionPressed("toggle_tensor_field"))
		{
			newState = SchemeState.TensorField;
		} else if (e.IsActionPressed("toggle_streamlines"))
		{
			newState = SchemeState.Streamlines;
		} else
		{
			return;
		}

		_cityScheme.Visible = (!_cityScheme.Visible || _cityScheme.SchemeState != newState);
		_cityScheme.SchemeState = newState;
		if (_cityScheme.Visible)
		{
			_cityScheme.QueueRedraw();
		}
	}
}
