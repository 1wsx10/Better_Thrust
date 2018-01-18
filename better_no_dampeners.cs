
// true: only main cockpit can be used even if there is no one in the main cockpit
// false: any cockpits can be used, but if there is someone in the main cockpit, it will only obey the main cockpit
// no main cockpit: any cockpits can be used
public const bool onlyMainCockpit = false;

// script will completely ignore any block with this in the name
// if you add this to a blocks name, you need to cause an update: recompile or change the ship mass
public const string ignore = "---";

















public Program() {
	oldMass = 0;
	Runtime.UpdateFrequency = UpdateFrequency.Update1;
}

public int programCounter = 0;
public void Main(string args, UpdateType asdf) {

	programCounter++;
	String spinner = "";
	switch(programCounter/10%4) {
		case 0:
			spinner = "|";
		break;
		case 1:
			spinner = "\\";
		break;
		case 2:
			spinner = "-";
		break;
		case 3:
			spinner = "/";
		break;
	}
	Echo($"{spinner}\nLast Runtime {Runtime.LastRunTimeMs.Round(2)}ms");


	//Echo("before setup");
	IMyShipController cont = findACockpit();
	if(!IsMassTheSame(cont)) {// this is null safe, doesn't call GTS, only checks PB memory for cockpit
		setup();//single, solitary call to GTS
	}
	if(cont == null || !cont.IsWorking) {
		cont = findACockpit();
		if(cont == null || !cont.IsWorking) {
			Echo("Exiting.");
			return;
		}
	}

	//Echo("After setup\nstarting to add grids");



	// dampeners
	if(mainController != null && mainController.DampenersOverride) {
		removeOverride();
		return;
	}
	bool dampeners = false;
	// check controllers
	foreach(var gridKV in grids) {
		Subgrid grid = gridKV.Value;
		foreach(IMyShipController controller in grid.controllers) {
			if(controller.DampenersOverride) {
				dampeners = true;
				break;
			}
		}
		if(dampeners) break;
	}
	// stop if dampeners
	if(dampeners) {
		foreach(var gridKV in grids) {
			Subgrid grid = gridKV.Value;
			foreach(IMyThrust thruster in grid.thrusters) {
				thruster.ThrustOverride = 0;
			}
		}
		return;
	}


	Vector3D grav = cont.GetNaturalGravity();
	MyShipMass shipMass = cont.CalculateShipMass();
	float mass = shipMass.PhysicalMass;

	Vector3D gravForce = grav * mass;

	//Echo($"Ship Mass: {mass}");
	//Echo($"Grav Acceleration: {grav.Round(0).Length().Round(0)}");

	Vector3D pos_gravForce = (gravForce + Vector3D.Abs(gravForce))/2;
	Vector3D neg_gravForce = gravForce - pos_gravForce;

	//Echo($"Grav Weight: {gravForce.Round(0).Length().Round(0)}");
	//Echo($"Grav Weight: {(pos_gravForce + neg_gravForce).Length().Round(0)}");

	// Echo($"pos gforce: {pos_gravForce.Round(0)}");
	// Echo($"neg gforce: {neg_gravForce.Round(0)}");

	Vector3D move = getMovement() * -1 * mass * 10000;

	// add up all max thrust
	// evenly spread gravity between them
	Vector3D pos_totalThrust = Vector3D.Zero;
	Vector3D neg_totalThrust = Vector3D.Zero;

	foreach(var gridKV in grids) {
		Subgrid grid = gridKV.Value;

		grid.calculateMaxThrust();

		pos_totalThrust += grid.pos_maxThrustWorld;
		neg_totalThrust += grid.neg_maxThrustWorld;
	}
	foreach(var gridKV in grids) {
		Subgrid grid = gridKV.Value;

		grid.pos_relThrust = (grid.pos_maxThrustWorld / pos_totalThrust).NaNtoZero();
		grid.neg_relThrust = (grid.neg_maxThrustWorld / neg_totalThrust).NaNtoZero();

		// Echo("\n");

		// Echo($"pos max world: {(grid.pos_maxThrustWorld / 100).Round(0) * 100}");
		// Echo($"neg max world: {(grid.neg_maxThrustWorld / 100).Round(0) * 100}");

		// Echo($"pos total thrust world: {(pos_totalThrust / 100).Round(0) * 100}");
		// Echo($"neg total thrust world: {(neg_totalThrust / 100).Round(0) * 100}");
		// Echo("\n");

		// Echo($"Grid Rel: {(grid.pos_relThrust + grid.neg_relThrust).Length().Round(1)}");
		// Echo($"Grid RelP: {grid.pos_relThrust.Round(1)}");
		// Echo($"Grid RelN: {grid.neg_relThrust.Round(1)}");

		Vector3D finalGrav = grid.pos_relThrust * pos_gravForce + grid.neg_relThrust * neg_gravForce;
		grid.go(finalGrav, move, mass);
		// Echo($"final req: {finalGrav.Round(0)}");
		// Echo(grid.errStr);
		// grid.errStr = "";
		//Echo("Setting Grid Thrust");
	}
	//Echo($"Program Counter: {programCounter}");
}

public Dictionary<IMyCubeGrid, Subgrid> grids = new Dictionary<IMyCubeGrid, Subgrid>();
public float oldMass;
public bool JustCompiled;
public IMyShipController mainController;

public void removeOverride() {
	foreach(var gridKV in grids) {
		Subgrid grid = gridKV.Value;
		foreach(IMyThrust thruster in grid.thrusters) {
			thruster.ThrustOverride = 0;
		}
	}
}

public bool IsMassTheSame(IMyShipController cont) {
	if(cont == null || !cont.IsWorking) {
		Echo("No active ship controller found.");
		return false;
	}
	var mass = cont.CalculateShipMass();
	bool output = Math.Abs(oldMass - mass.BaseMass) < 0.01;
	if(!output) {
		Echo($"*mass not the same, performing setup.\ndiff: {Math.Abs(oldMass - mass.BaseMass).Round(3)}");
	}
	oldMass = mass.BaseMass;
	return output;
}

public void setup() {
	List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
	GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks, block => (block is IMyShipController || block is IMyThrust) && !block.CustomName.ToLower().Contains(ignore));
	Echo($"*count: {blocks.Count}");

	grids.Clear();

	foreach(IMyTerminalBlock block in blocks) {
		if(!grids.ContainsKey(block.CubeGrid)) {

			// controller
	        	if(block is IMyShipController) {
				var conts = new HashSet<IMyShipController>();
				conts.Add((IMyShipController)block);
	        		grids[block.CubeGrid] = new Subgrid(this, conts);

		        	// main cockpit
				if(((IMyShipController)block).IsMainCockpit) {
					mainController = (IMyShipController)block;
				}
			}

			// thruster
	        	if(block is IMyThrust) {
				var thrusts = new HashSet<IMyThrust>();
				thrusts.Add((IMyThrust)block);
	       			grids[block.CubeGrid] = new Subgrid(this, thrusts);
	        	}
		} else {
		        grids[block.CubeGrid].Add(block);
		}

	}


}

public IMyShipController findMainCockpit() {
	if(mainController.IsMainCockpit) return mainController;

	foreach(var gridKV in grids) {
		foreach(IMyShipController cont in gridKV.Value.controllers) {
			if(cont.IsMainCockpit) return mainController;
		}
	}

	return null;
}

public IMyShipController findACockpit() {
	foreach(var gridKV in grids) {
		foreach(IMyShipController cont in gridKV.Value.controllers) {
			if(cont.IsWorking) {
				return cont;
			}
		}
	}

	return null;
}

public Vector3D getMovement() {
	// movement controls
	Vector3D moveVec = Vector3D.Zero;

	foreach(var gridKV in grids) {
		Subgrid grid = gridKV.Value;

		if(mainController != null && (mainController.IsUnderControl || onlyMainCockpit)) {
			if(!grid.controllers.Contains(mainController)) return Vector3D.Zero;
			moveVec = mainController.getWorldMoveIndicator();
		} else {
			foreach(IMyShipController cont in grid.controllers) {
				if(cont.IsUnderControl) {
					moveVec += cont.getWorldMoveIndicator();
				}
			}
		}
	}

	return moveVec;
}

public class Subgrid {
	public Program program;

	public IMyCubeGrid grid;
	public HashSet<IMyThrust> thrusters;
	public HashSet<IMyShipController> controllers;

	public Vector3D pos_maxThrust = Vector3D.Zero;
	public Vector3D neg_maxThrust = Vector3D.Zero;

	public Vector3D pos_maxThrustWorld = Vector3D.Zero;
	public Vector3D neg_maxThrustWorld = Vector3D.Zero;

	public Vector3D neg_relThrust = Vector3D.Zero;
	public Vector3D pos_relThrust = Vector3D.Zero;

	// public string errStr = "";

	public Subgrid(Program prog, IMyCubeGrid grid, HashSet<IMyThrust> thrusters, HashSet<IMyShipController> controllers) {
		this.program = prog;
		this.grid = grid;
		this.thrusters = thrusters;
		this.controllers = controllers;
	}

	public Subgrid(Program prog, IMyCubeGrid grid) {
		this.program = prog;
		this.grid = grid;
		this.thrusters = new HashSet<IMyThrust>();
		this.controllers = new HashSet<IMyShipController>();
	}

	public Subgrid(Program prog, HashSet<IMyThrust> thrusters) {
		this.program = prog;
		this.thrusters = thrusters;
		this.controllers = new HashSet<IMyShipController>();

		foreach(IMyThrust thruster in thrusters) {
			this.grid = thruster.CubeGrid;
			return;
		}
	}

	public Subgrid(Program prog, HashSet<IMyShipController> controllers) {
		this.program = prog;
		this.thrusters = new HashSet<IMyThrust>();
		this.controllers = controllers;

		foreach(IMyShipController cont in controllers) {
			this.grid = cont.CubeGrid;
			return;
		}
	}

	public void go(Vector3D requiredVec, Vector3D move, float mass) {
		//make each one then do its own combined moveIndicator
		Vector3D positive = new Vector3D(1, 1, 1);


		foreach(IMyThrust thruster in thrusters) {
			Vector3D fullThrustInEngineDirection = thruster.Orientation.Forward.GetVector();

			if(positive.dot(fullThrustInEngineDirection) > 0) {
				fullThrustInEngineDirection *= pos_maxThrust;
			} else {
				// side effect, this also acts like ABS
				fullThrustInEngineDirection *= neg_maxThrust;
			}

			double rel = thruster.MaxEffectiveThrust / fullThrustInEngineDirection.Length();

			// compensation for lower efficiency
			double comp = thruster.MaxThrust / thruster.MaxEffectiveThrust;

			// finally, fire the thruster
			thruster.setThrust(rel * (move + requiredVec) * comp);

			// data reporting
			// Vector3D proj = requiredVec.project(thruster.WorldMatrix.Backward);
			// double required = (proj.dot(thruster.WorldMatrix.Backward) > 0) ? (proj.Length().Round(0)) : 0;
			// errStr += $"\nthruster: {thruster.CustomName}\nbackward: {thruster.WorldMatrix.Backward.Round(1)}\nrequired: {required}\nrelative for this direction: {rel.Round(1)}\nmax: {thruster.MaxEffectiveThrust.Round(0)}\nefficiency compensation: {comp.Round(1)}\n";
		}
	}

	// grid coordinates, not world coordinates
	public void calculateMaxThrust() {
		pos_maxThrustWorld = Vector3D.Zero;
		neg_maxThrustWorld = Vector3D.Zero;
		pos_maxThrust = Vector3D.Zero;
		neg_maxThrust = Vector3D.Zero;

		foreach(IMyThrust thruster in thrusters) {
			Vector3D engineDir = thruster.Orientation.Forward.GetVector();

			Vector3D engineForce = engineDir * thruster.MaxEffectiveThrust;
			Vector3D engineForceWorld = engineForce.TransformNormal(grid.WorldMatrix);

			// split into positive and negative
			Vector3D positive = (engineForce + engineForce.Abs())/2;
			pos_maxThrust += positive;
			neg_maxThrust += engineForce - positive;

			// and for world space too
			positive = (engineForceWorld + engineForceWorld.Abs())/2;
			pos_maxThrustWorld += positive;
			neg_maxThrustWorld += engineForceWorld - positive;
		}
	}

	public void Add(IMyTerminalBlock block) {
		if(block is IMyThrust) {
			thrusters.Add((IMyThrust)block);
		}
		if(block is IMyShipController) {
			controllers.Add((IMyShipController)block);
		}
	}

	public IMyShipController getMainCockpit() {
		foreach(IMyShipController cont in controllers) {
			if(cont.IsMainCockpit) return cont;
		}
		return null;
	}
}

}
public static class CustomProgramExtensions {

	public static double dot(this Vector3D a, Vector3D b) {
		return Vector3D.Dot(a, b);
	}

	public static Vector3D project(this Vector3D a, Vector3D b) {
		double adb = a.dot(b);
		double bdb = b.dot(b);
		return b * adb / bdb;
	}

	public static Vector3D reject(this Vector3D a, Vector3D b) {
		return Vector3D.Reject(a, b);
	}

	public static Vector3D Normalized(this Vector3D vec) {
		return Vector3D.Normalize(vec);
	}

	public static void setThrust(this IMyThrust thruster, Vector3D desired) {
		var proj = desired.project(thruster.WorldMatrix.Backward);

		if(proj.dot(thruster.WorldMatrix.Backward) > 0) {//negative * negative is positive... so if its greater than 0, you ignore it.
			thruster.ThrustOverride = 0;
			return;
		}

		thruster.ThrustOverride = (float)proj.Length();
	}

	public static void setThrust(this IMyThrust thruster, Vector3D desired, out string error) {
		error = "";
		var proj = desired.project(thruster.WorldMatrix.Backward);

		if(proj.dot(thruster.WorldMatrix.Backward) > 0) {//negative * negative is positive... so if its greater than 0, you ignore it.
			thruster.ThrustOverride = 0;
			error += "wrong way";
			return;
		}
		error += $"right way";
		error += $"\nproportion: {(proj.Length() / desired.Length()).Round(2)}";
		error += $"\nproj: {proj.Length().Round(1)}";
		error += $"\ndes: {desired.Length().Round(1)}";

		error += $"\nproj: {proj.Round(1)}";
		error += $"\ndesired: {desired.Round(1)}";

		thruster.ThrustOverride = (float)proj.Length();
	}

	public static Vector3D Abs(this Vector3D vec) {
		return Vector3D.Abs(vec);
	}

	public static Vector3D Round(this Vector3D vec, int num) {
		return Vector3D.Round(vec, num);
	}

	public static double Round(this double val, int num) {
		return Math.Round(val, num);
	}

	public static float Round(this float val, int num) {
		return (float)Math.Round(val, num);
	}

	public static Vector3D getWorldMoveIndicator(this IMyShipController controller) {
		return Vector3D.TransformNormal(controller.MoveIndicator, controller.WorldMatrix);
	}

	public static bool IsNaN(this double val) {
		return double.IsNaN(val);
	}

	public static Vector3D NaNtoZero(this Vector3D val) {
		if(val.X.IsNaN()) {
			val.X = 0;
		}
		if(val.Y.IsNaN()) {
			val.Y = 0;
		}
		if(val.Z.IsNaN()) {
			val.Z = 0;
		}

		return val;
	}

	public static Vector3 GetVector(this Base6Directions.Direction dir) {
		return Base6Directions.GetVector(dir);
	}

	public static Vector3D TransformNormal(this Vector3D vec, MatrixD mat) {
		return Vector3D.TransformNormal(vec, mat);
	}
