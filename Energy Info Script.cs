Program(){
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

public double   getMedH2Capacity(){	
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

public bool     isOnThisGrid(IMyCubeGrid G){
	if (G == Me.CubeGrid) return true;
	else return false;
}

public String   toPercent(float up, float down){
    float input = (100 * up) / down;
    String output = input.ToString("0.00");
    return output;
}

public String   printEnergyInfo(){
    float ShipsStoredPower = 0;
    float ShipsMaxPower = 0;
    float MaxShipOutput = 0;
    float CurrentBatteryOutput = 0;
    float CurrentShipOutput = 0;
    int Online = 0;
    int Recharging = 0;
    int Empty = 0;
    int Offline = 0;
    int RNominal = 0;
    int ROff = 0;

    List<IMyPowerProducer> Producers = new List<IMyPowerProducer>();
    GridTerminalSystem.GetBlocksOfType<IMyPowerProducer>(Producers);

    foreach (IMyPowerProducer P in Producers){
        if (isOnThisGrid(P.CubeGrid)){
        	if (P.IsWorking) MaxShipOutput += P.MaxOutput;
       		CurrentShipOutput += P.CurrentOutput;

        	if (P is IMySolarPanel) { }
        	else if (!(P is IMyBatteryBlock)){
         	   if (P.IsWorking) RNominal++;
         	   else ROff++;
        	}
        	else{
        	    IMyBatteryBlock B = (IMyBatteryBlock)P;
        	    ShipsStoredPower += B.CurrentStoredPower;
         	    ShipsMaxPower += B.MaxStoredPower;
	 	        CurrentBatteryOutput += B.CurrentOutput;	
         	    CurrentShipOutput -= B.CurrentInput;
	 	        CurrentBatteryOutput -= B.CurrentInput;

         	    if (B.CurrentStoredPower == 0) Empty++;
         	    else if (!(B.IsWorking)) Offline++;
         	    else if (B.ChargeMode == ChargeMode.Recharge) Recharging++;
         	    else Online++;
        	}
	    }
    }

    float convert = 1.0F;//((float)10 / (float)36); //<- to jest w kWs

    CurrentShipOutput = convert * CurrentShipOutput;
    MaxShipOutput = convert * MaxShipOutput;

    String output = "\n Current Output: " + CurrentShipOutput.ToString("0.00") + "/" + MaxShipOutput.ToString("0.0") +
    " MWh (" + toPercent(CurrentShipOutput, MaxShipOutput) + "%)";
	
	if(CurrentBatteryOutput == 0){
		output += "\n Batteries in equilibrium.";
	}
	else{
    	float remainingTime = (ShipsStoredPower * 1000) / CurrentBatteryOutput;
	if(remainingTime < 0){
		output += "\n Recharged in     ";
		remainingTime *= -1;
    		if (remainingTime > 3600)
    		{
        		output += (remainingTime / 3600).ToString("0.") + " h ";
        		remainingTime = remainingTime % 3600;
        		output += (remainingTime / 60).ToString("0.") + " m ";
        		remainingTime = remainingTime % 60;
        		output += remainingTime.ToString("0.") + " s";
    		}
   		else if (remainingTime > 60)
    		{
			output += (remainingTime / 60).ToString("0.") + " m ";
        		remainingTime = remainingTime % 60;
        		output += remainingTime.ToString("0.") + " s";
    		}    
    		else
    		{
        		output += remainingTime.ToString("0.") + " s";
    		}
	}   
    else{	 
    		output += "\n Will last for       ";
    		if (remainingTime > 3600)
    		{
        		output += (remainingTime / 3600).ToString("0.") + " h ";
        		remainingTime = remainingTime % 3600;
        		output += (remainingTime / 60).ToString("0.") + " m ";
        		remainingTime = remainingTime % 60;
        		output += remainingTime.ToString("0.") + " s";
    		}
   		else if (remainingTime > 60)
    		{
			output += (remainingTime / 60).ToString("0.") + " m ";
        		remainingTime = remainingTime % 60;
        		output += remainingTime.ToString("0.") + " s";
    		}    
    		else
    		{
        		output += remainingTime.ToString("0.") + " s";
    		}
    	}
	}

    if (RNominal > 0 || ROff > 0) {
        double percent = getMedH2Capacity();
        output += "\n Cores Online:    " + RNominal + "/" + (RNominal + ROff);
    }
    else output += "\n No power cores present!";

    output += "\n Batteries:          " + Online + "/" + (Online + Empty + Recharging + Offline) + " Online";
    if (Recharging > 0) output += "\n                           " + Recharging + " Recharging";
    if (Empty > 0) output += "\n                           " + Empty + " Empty";

    return output;
}

public List<IMyTextPanel> getEnergyScreen(){
	List<IMyTextPanel> output = new List<IMyTextPanel>();
	List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
	GridTerminalSystem.SearchBlocksOfName("#EnergyScreen", temp);
	
	foreach(IMyTerminalBlock b in temp){
		if(b is IMyTextPanel && isOnThisGrid(b.CubeGrid)) {
    		IMyTextPanel tempo = b as IMyTextPanel;
    		output.Add(tempo);
		}
	}
	temp.Clear();
	
	return output;
}

public void     Output(String output){
    foreach(IMyTextPanel EnergyScreen in getEnergyScreen()){
        EnergyScreen.FontSize = (float)1.9;
        EnergyScreen.WriteText(output, false);
    }
}

public void     Main(string argument, UpdateType updateSource){
    Output(printEnergyInfo());
}