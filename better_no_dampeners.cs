

public bool standby = true;

public Program() {
	justCompiled = true;
	oldMass = 0;
}

public void Main(string args, UpdateType asdf) {
	if(!IsMassTheSame(grids[0].controllers)) {
		setup();
	}
	justCompiled = false;
}

public void Save() {}

public Dictionary<IMyCubeGrid, Subgrid> grids = new List<Subgrid>();
public float oldMass;
public bool justCompiled;
public IMyShipController mainCockpit;

public bool IsMassTheSame(IMyShipController cont) {
	if(!JustCompiled) return false;
	var mass = cont.CalculateShipMass();
	return oldMass == mass.BaseMass;
}

public void setup() {
	List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
	GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks);

	thrusters.Clear();
	controllers.Clear();

	foreach(IMyTerminalBlock block in blocks) {
		if(grids[block.CubeGrid] == null) {
			if(block is IMyShipController) {
				grids[block.CubeGrid] = new Subgrid(new HashSet<IMyShipController>((IMyShipController)block));
			} else if(block is IMyThrust) {
				grids[block.CubeGrid] = new Subgrid(new HashSet<IMyThrust>((IMyThrust)block));
			}
		} else {
			grids[block.CubeGrid].Add(block);
		}
	}
}

void addBlock<T>(Collection<T> container, IMyTerminalBlock block) {
	if(block
}

public IMyShipController findMainCockpit() {
	// TODO
}

public class Subgrid {
	public IMyCubeGrid grid;
	public HashSet<IMyThrust> thrusters;
	public HashSet<IMyShipController> controllers;

	public Subgrid(IMyCubeGrid grid, HashSet<IMyThrust> thrusters, HashSet<IMyShipController> controllers) {
		this.grid = grid;
		this.thrusters = thrusters;
		this.controllers = controllers;
	}

	public Subgrid(IMyCubeGrid grid) {
		this.grid = grid;
		this.thrusters = new HashSet<IMyThrust>();
		this.controllers = new HashSet<IMyShipController>();
	}

	public Subgrid(HashSet<IMyThrust> thrusters) {
		this.thrusters = thrusters;
		this.controllers = new HashSet<IMyShipController>();

		foreach(IMyThrust thruster in thrusters) {
			this.grid = thruster.CubeGrid;
			return;
		}
	}

	public Subgrid(HashSet<IMyShipController> controllers) {
		this.thrusters = new HashSet<IMyThrust>();
		this.controllers = controllers;

		foreach(IMyShipController cont in controllers) {
			this.grid = cont.CubeGrid;
			return;
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
