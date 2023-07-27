using Godot;

public partial class Main : Node3D
{
	private ModelGenerator _roads;
	private CityScheme _cityScheme;
	private Camera _camera;
	private Vector2 _oldUserPosition;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_roads = GetNode("./Roads") as ModelGenerator;
		_cityScheme = GetNode("./CityScheme") as CityScheme;
		_camera = GetNode("./Camera3D") as Camera;
	}

	public override void _Process(double delta)
	{
		_cityScheme.UserPosition = new Vector2(_camera.Transform.Origin.X, _camera.Transform.Origin.Z) / _roads.ModelScale;
		if ((_cityScheme.UserPosition - _oldUserPosition).LengthSquared() >= 50)
		{
			_oldUserPosition = _cityScheme.UserPosition;
			_cityScheme.QueueRedraw();
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Input(InputEvent e)
	{
		if (e.IsActionPressed("debug_none"))
		{
			_roads.GenerateRoads(DebugType.None);
		} else if (e.IsActionPressed("debug_highlight_roads"))
		{
			_roads.GenerateRoads(DebugType.HighlightRoads);
		} else if (e.IsActionPressed("debug_highlight_triangles"))
		{
			_roads.GenerateRoads(DebugType.HighlightTriangles);
		} else if (e.IsActionPressed("toggle_city_scheme_visibility"))
		{
			_cityScheme.Visible = !_cityScheme.Visible;
		} else if (e.IsActionPressed("toggle_tensor_field"))
		{
			_cityScheme.ShouldDrawTensorField = !_cityScheme.ShouldDrawTensorField;
			_cityScheme.QueueRedraw();
		} else if (e.IsActionPressed("toggle_flight_mode"))
		{
			_camera.FlightMode = !_camera.FlightMode;
			if (!_camera.FlightMode)
			{
				var transform = _camera.Transform;
				transform.Basis = Basis.Identity;
				_camera.Transform = transform;
			}
		}
	}
}
