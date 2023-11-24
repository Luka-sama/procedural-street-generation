using Godot;

public partial class Main : Node3D
{
	private ModelGenerator _roads;
	private CityScheme _cityScheme;
	private Vector2 _oldUserPosition;
	private CharacterBody3D _player;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_roads = GetNode<ModelGenerator>("%Roads");
		_cityScheme = GetNode<CityScheme>("%CityScheme");
		_player = GetNode<CharacterBody3D>("%Player");
	}

	public override void _Process(double delta)
	{
		_cityScheme.UserPosition = new Vector2(_player.Transform.Origin.X, _player.Transform.Origin.Z) / _roads.ModelScale;
		if ((_cityScheme.UserPosition - _oldUserPosition).LengthSquared() >= 50)
		{
			_oldUserPosition = _cityScheme.UserPosition;
			_cityScheme.QueueRedraw();
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Input(InputEvent e)
	{
		if (e.IsActionPressed("toggle_city_scheme_visibility"))
		{
			_cityScheme.Visible = !_cityScheme.Visible;
		} else if (e.IsActionPressed("toggle_tensor_field"))
		{
			_cityScheme.ShouldDrawTensorField = !_cityScheme.ShouldDrawTensorField;
			_cityScheme.QueueRedraw();
		} else if (Input.IsKeyPressed(Key.J))
		{
			_cityScheme.WithGraph = !_cityScheme.WithGraph;
			_cityScheme.QueueRedraw();
		}
	}
}
