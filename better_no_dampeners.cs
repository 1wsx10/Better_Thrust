
// true: only main cockpit can be used even if there is no one in the main cockpit
// false: any cockpits can be used, but if there is someone in the main cockpit, it will only obey the main cockpit
// no main cockpit: any cockpits can be used
public const bool onlyMainCockpit = false;

















public Program() {
	JustCompiled = true;
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
	Echo(spinner);


	//Echo("before setup");
	if(!IsMassTheSame(findACockpit())) {// this is null safe
		setup();
	}
	IMyShipController cont = findACockpit();
	if(cont == null) return;

	//Echo("After setup\nstarting to add grids");



	// dampeners
	if(mainController != null && mainController.DampenersOverride) {
		foreach(var gridKV in grids) {
			Subgrid grid = gridKV.Value;
			foreach(IMyThrust thruster in grid.thrusters) {
				thruster.ThrustOverride = 0;
			}
		}
		return;
	}
	foreach(var gridKV in grids) {
		Subgrid grid = gridKV.Value;
		foreach(IMyThrust thruster in grid.thrusters) {
			thruster.ThrustOverride = 0;
		}
		foreach(IMyShipController controller in grid.controllers) {
			if(controller.DampenersOverride) {
				return;
			}
		}
	}


	Vector3D grav = cont.GetNaturalGravity();
	MyShipMass shipMass = cont.CalculateShipMass();
	float mass = shipMass.PhysicalMass;

	Vector3D gravForce = grav * mass;

	//Echo($"Ship Mass: {mass}");
	//Echo($"Grav Acceleration: {grav.Round(0).Length().Round(0)}");

	Vector3D pos_gravForce = (gravForce - Vector3D.Abs(gravForce))/2;
	Vector3D neg_gravForce = gravForce - pos_gravForce;

	//Echo($"Grav Weight: {gravForce.Round(0).Length().Round(0)}");
	//Echo($"Grav Weight: {(pos_gravForce + neg_gravForce).Length().Round(0)}");


	// add up all max thrust
	// evenly spread gravity between them
	Vector3D pos_totalThrust = Vector3D.Zero;
	Vector3D neg_totalThrust = Vector3D.Zero;

	foreach(var gridKV in grids) {
		var grid = gridKV.Value;

		grid.calculateMaxThrust();
		grid.pos_maxThrustWorld = grid.pos_maxThrust.TransformNormal(grid.grid.WorldMatrix);
		grid.neg_maxThrustWorld = grid.neg_maxThrust.TransformNormal(grid.grid.WorldMatrix);

		pos_totalThrust += grid.pos_maxThrustWorld;
		neg_totalThrust += grid.neg_maxThrustWorld;
	}
	foreach(var gridKV in grids) {
		var grid = gridKV.Value;

		grid.pos_relThrust = grid.pos_maxThrustWorld / pos_totalThrust;
		grid.neg_relThrust = grid.neg_maxThrustWorld / neg_totalThrust;

		grid.pos_relThrust = grid.pos_relThrust.NaNtoZero();
		grid.neg_relThrust = grid.neg_relThrust.NaNtoZero();

		//Echo($"Grid Rel: {(grid.pos_relThrust + grid.neg_relThrust).Length().Round(0)}");
		//Echo($"Grid RelP: {grid.pos_relThrust.Round(0)}");
		//Echo($"Grid RelN: {grid.neg_relThrust.Round(0)}");

		Vector3D finalReq = grid.pos_relThrust * pos_gravForce + grid.neg_relThrust * neg_gravForce;
		grid.go(finalReq, mass);
		//Echo($"final req: {finalReq.Length().Round(0)}");
		//Echo(grid.errStr);
		grid.errStr = "";
		//Echo("Setting Grid Thrust");
	}
	//Echo($"Program Counter: {programCounter}");



	JustCompiled = false;
}

public Dictionary<IMyCubeGrid, Subgrid> grids = new Dictionary<IMyCubeGrid, Subgrid>();
public float oldMass;
public bool JustCompiled;
public IMyShipController mainController;

public bool IsMassTheSame(IMyShipController cont) {
	if(!JustCompiled) return false;
	if(cont == null || !cont.IsWorking) return false;
	var mass = cont.CalculateShipMass();
	return oldMass == mass.BaseMass;
}

public void setup() {
	List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
	GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks, block => block is IMyShipController || block is IMyThrust);

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

	public string errStr = "";

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

	public void go(Vector3D requiredVec, float mass) {
		//make each one then do its own combined moveIndicator
		Vector3D move = getMovement() * -1 * mass * 10000;
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

			//string thrustErr;
			thruster.setThrust(rel * (move + requiredVec) * thruster.MaxThrust / thruster.MaxEffectiveThrust);
			//errStr += $"\nthruster: {thruster.CustomName}\nreq: {requiredVec.Length().Round(0)}\nrel: {rel.Round(1)}\nmax: {thruster.MaxEffectiveThrust.Round(0)}\nmaxthr: {(pos_maxThrust + neg_maxThrust).Length().Round(1)}\n{thrustErr}\n";
		}
	}

	// grid coordinates, not world coordinates
	public void calculateMaxThrust() {
		Vector3D positive = new Vector3D(1, 1, 1);
		pos_maxThrust = Vector3D.Zero;
		neg_maxThrust = Vector3D.Zero;

		foreach(IMyThrust thruster in thrusters) {
			Vector3 engineDir = thruster.Orientation.Forward.GetVector();

			if(positive.dot(engineDir) > 0) {
				pos_maxThrust += engineDir * thruster.MaxEffectiveThrust;
			} else {
				neg_maxThrust += engineDir * thruster.MaxEffectiveThrust;
			}
		}
	}

	public Vector3D getMovement() {
		// movement controls
		Vector3D moveVec = Vector3D.Zero;

		if(program.mainController != null && (program.mainController.IsUnderControl || onlyMainCockpit)) {
			if(!controllers.Contains(program.mainController)) return Vector3D.Zero;
			moveVec = program.mainController.getWorldMoveIndicator();
		} else {
			foreach(IMyShipController cont in controllers) {
				if(cont.IsUnderControl) {
					moveVec += cont.getWorldMoveIndicator();
				}
			}
		}

		return moveVec;
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
