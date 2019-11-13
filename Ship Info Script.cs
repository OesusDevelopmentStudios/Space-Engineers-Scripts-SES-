Program(){
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

/// Support Functions

public bool isOnThisGrid(IMyCubeGrid G){
	if (G == Me.CubeGrid) return true;
	else return false;
}

public String toPercent(float up, float down)
{
    float input = (100 * up) / down;
    String output = input.ToString("0.0");
    return output;
}

public void output(string output){
	List<IMyTextPanel> screens = new List<IMyTextPanel>();
	List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
	GridTerminalSystem.SearchBlocksOfName("#ShipInfoScreen", temp);
	
	foreach(IMyTerminalBlock b in temp){
		if(b is IMyTextPanel && isOnThisGrid(b.CubeGrid)) {
    		IMyTextPanel tempo = b as IMyTextPanel;
    		screens.Add(tempo);
		}
	}
	temp.Clear();

	foreach(IMyTextPanel screen in screens){
    	screen.FontSize = (float)1.6;
    	screen.WriteText(output,false);
	}
}

public double getMedH2Capacity(){	
	List<IMyGasTank> temp = new List<IMyGasTank>();
	List<IMyGasTank> tank = new List<IMyGasTank>();
	int counter = 0;
	double output = 0;
	GridTerminalSystem.GetBlocksOfType<IMyGasTank> (temp);
	foreach(IMyGasTank t in temp){
		if(t.Capacity>100000f && isOnThisGrid(t.CubeGrid)) {
			tank.Add(t);
			counter++;
		}
	}
	if (counter == 0) return 0D;
	else{
		Double tempo = Convert.ToDouble(counter);
		foreach(IMyGasTank t in tank){
			output += t.FilledRatio;
		}
		return (output/tempo);
	}
}

/// Main Functions

public string printStatus(){
    string output ="";

    List<IMyPowerProducer> Producers = new List<IMyPowerProducer>();
    GridTerminalSystem.GetBlocksOfType<IMyPowerProducer>(Producers);
    float ShipsStoredPower = 0;
    float ShipsMaxPower = 0;
    foreach (IMyPowerProducer P in Producers){
        if (isOnThisGrid(P.CubeGrid)){

        	if (P is IMySolarPanel) { }
        	else if (!(P is IMyBatteryBlock)){ }
        	else{
        	    IMyBatteryBlock B = (IMyBatteryBlock)P;
        	    ShipsStoredPower += B.CurrentStoredPower;
         	    ShipsMaxPower += B.MaxStoredPower;
        	}
	    }
    }
    output =  "\n Current Power:\n   " + ShipsStoredPower.ToString("0.0") + "/" + ShipsMaxPower.ToString("0.0") + " MW (" 
    + toPercent(ShipsStoredPower, ShipsMaxPower) + "%)";

    output += "\n Hydrogen Reserves:\n   " + (getMedH2Capacity()*100).ToString("0.00") + "%";

    output += "\n Ship's location:" 
        + "\n   X:" + Me.GetPosition().X.ToString("0.00") 
        + "\n   Y:" + Me.GetPosition().Y.ToString("0.00") 
        + "\n   Z:" + Me.GetPosition().Z.ToString("0.00");

    return output;
}

public void     Main(string argument, UpdateType updateSource){
    output(printStatus());
}