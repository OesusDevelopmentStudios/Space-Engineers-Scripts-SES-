Program(){
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

/// Support Functions

public void output(string output){
    IMyTextPanel screen = GridTerminalSystem.GetBlockWithName("#ShipInfoScreen");
    screen.WriteText(output,false);
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
    output =  "\n Current Power: " + ShipsStoredPower * 1000 + "/" + ShipsMaxPower * 1000 + " kW (" 
    + toPercent(ShipsStoredPower, ShipsMaxPower) + "%)";

    output += "\n Hydrogen Reserves: " + getMedH2Capacity() + "%";

    output += "\n Ship's location: " + (Me.GetPosition()).ToString();

    return output;
}

public void     Main(string argument, UpdateType updateSource){
    output(printStatus());
}