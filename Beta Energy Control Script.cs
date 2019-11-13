Program(){
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}


public int lastDropCounter = 0;
public int lastUpCounter = 0;

public string LastState = "NULL";

public bool isOnThisGrid(IMyCubeGrid G){
	if (G == Me.CubeGrid) return true;
	else return false;
}

public float getOutputPercent(){
    float CurrentShipOutput = 0, MaxShipOutput = 0;
    List<IMyPowerProducer>   Producers  = new List<IMyPowerProducer>();
    GridTerminalSystem.GetBlocksOfType<IMyPowerProducer> (Producers);
    List<IMyBatteryBlock>   Batteries = new List<IMyBatteryBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock> (Batteries);

        foreach (IMyPowerProducer P in Producers){
            if(P.IsWorking) MaxShipOutput += P.MaxOutput;
            CurrentShipOutput  += P.CurrentOutput;
        }

        foreach (IMyBatteryBlock B in Batteries){
            CurrentShipOutput   -= B.CurrentInput;
        }

        if(MaxShipOutput>0) return 100*CurrentShipOutput/MaxShipOutput;
        else return 0;
}

public float getPowerPercent(){
    float max = 0, curr = 0;

    List<IMyBatteryBlock>   Batteries = new List<IMyBatteryBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock> (Batteries);

    foreach (IMyBatteryBlock B in Batteries){
            curr += B.CurrentStoredPower;
            max += B.MaxStoredPower;
    }
    if(max>0) return 100*curr/max;
    else return 0;
}

public void setProduction (bool set){
        List<IMyProductionBlock>   Producers  = new List<IMyProductionBlock>();
        GridTerminalSystem.GetBlocksOfType<IMyProductionBlock> (Producers);
	
	
        List<IMyProductionBlock>   temp  = new List<IMyProductionBlock>();
	foreach(IMyProductionBlock P in Producers){
            if(isOnThisGrid(P.CubeGrid)) temp.Add(P);
        }
	
        foreach(IMyProductionBlock P in temp){
            P.Enabled = set;
        }
}

public bool getProduction (){
        List<IMyProductionBlock>   Producers  = new List<IMyProductionBlock>();
        GridTerminalSystem.GetBlocksOfType<IMyProductionBlock> (Producers);
	
	
        List<IMyProductionBlock>   temp  = new List<IMyProductionBlock>();
	foreach(IMyProductionBlock P in Producers){
            if(isOnThisGrid(P.CubeGrid)) temp.Add(P);
	}
	
        foreach(IMyProductionBlock P in temp){
            if(P.Enabled==true) return true;
        }
        return false;
}

public void setH2 (bool set){
        List<IMyPowerProducer>   Producers  = new List<IMyPowerProducer>();
        GridTerminalSystem.GetBlocksOfType<IMyPowerProducer> (Producers);
	
        List<IMyPowerProducer>   temp  = new List<IMyPowerProducer>();
	foreach(IMyPowerProducer P in Producers){
            if(isOnThisGrid(P.CubeGrid)) temp.Add(P);
	}
	
        foreach(IMyPowerProducer P in temp){
            if(!(P is IMyReactor)&&!(P is IMyBatteryBlock)) P.Enabled = set;
        }
}

public bool getH2 (){
        List<IMyPowerProducer>   Producers  = new List<IMyPowerProducer>();
        GridTerminalSystem.GetBlocksOfType<IMyPowerProducer> (Producers);
	
        List<IMyPowerProducer>   temp  = new List<IMyPowerProducer>();
	foreach(IMyPowerProducer P in Producers){
            if(isOnThisGrid(P.CubeGrid)) temp.Add(P);
	}	
	
        foreach(IMyPowerProducer P in temp){
            if(!(P is IMyReactor)&&!(P is IMyBatteryBlock)) if(P.Enabled==false) return false;
        }
        return true;
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

public void setReactors (bool set){
        List<IMyReactor>   Reactors  = new List<IMyReactor>();
        GridTerminalSystem.GetBlocksOfType<IMyReactor> (Reactors);
	
        List<IMyReactor>   temp  = new List<IMyReactor>();
	foreach(IMyReactor P in Reactors){
            if(isOnThisGrid(P.CubeGrid)) temp.Add(P);
	}
	
        foreach(IMyReactor P in temp){
            P.Enabled = set;
        }
}

public bool getReactors (){
        List<IMyReactor>   Reactors  = new List<IMyReactor>();
        GridTerminalSystem.GetBlocksOfType<IMyReactor> (Reactors);
	
        List<IMyReactor>   temp  = new List<IMyReactor>();
	foreach(IMyReactor P in Reactors){
            if(isOnThisGrid(P.CubeGrid)) temp.Add(P);
	}
	
        foreach(IMyReactor P in temp){
            if(P.Enabled == false) return false;
        }
        return true;
}

public void setBatteries (bool set){
        List<IMyBatteryBlock>   Batteries  = new List<IMyBatteryBlock>();
        GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock> (Batteries);
	
        List<IMyBatteryBlock>   temp  = new List<IMyBatteryBlock>();
	foreach(IMyBatteryBlock P in Batteries){
            if(isOnThisGrid(P.CubeGrid)) temp.Add(P);
	}
	
        foreach(IMyBatteryBlock B in temp){
            if(!set){
                B.ChargeMode = ChargeMode.Recharge;
            }
            else{
                B.ChargeMode = ChargeMode.Auto;
            }
       }
}

public bool getBatteries (){
        List<IMyBatteryBlock>   Batteries  = new List<IMyBatteryBlock>();
        GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock> (Batteries);
        foreach(IMyBatteryBlock B in Batteries){
            if(B.ChargeMode == ChargeMode.Recharge || B.Enabled == false) return false;
       }
        return true;
}

public void enableBatteries (bool set){
        List<IMyBatteryBlock>   Batteries  = new List<IMyBatteryBlock>();
        GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock> (Batteries);
        foreach(IMyBatteryBlock B in Batteries){
            B.Enabled = set;
       }
}

public bool checkIfEmergency(){
    if(getPowerPercent() < 5f && getMedH2Capacity()<10f) return true;
    else return false;
}

public void Emergency(){
    LastState = "EMERGENCY";
    setReactors(true);
    List<IMyFunctionalBlock> Everything = new List<IMyFunctionalBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyFunctionalBlock> (Everything);
	
        List<IMyFunctionalBlock>   temp  = new List<IMyFunctionalBlock>();
	foreach(IMyFunctionalBlock P in Everything){
            if(isOnThisGrid(P.CubeGrid)) temp.Add(P);
	}
	
    foreach(IMyFunctionalBlock A in temp){
        if(!(A is IMyReactor) && !(A is IMyShipConnector) && !(A is IMyShipMergeBlock) 
		&& !(A is IMyTextPanel) && !(A is IMyUpgradeModule) && !(A is IMyTextSurface)
		&& !(A is IMyProgrammableBlock) && !(A is IMyTimerBlock))
        {
            A.Enabled = false;
        }
    }
}

public void ClearEmergency(){
    
    setReactors(true);
    List<IMyFunctionalBlock> Everything = new List<IMyFunctionalBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyFunctionalBlock> (Everything);
	
        List<IMyFunctionalBlock>   temp  = new List<IMyFunctionalBlock>();
	foreach(IMyFunctionalBlock P in Everything){
            if(isOnThisGrid(P.CubeGrid)) temp.Add(P);
	}
	
    foreach(IMyFunctionalBlock A in temp){
        if(!(A is IMyReactor) && !(A is IMyShipConnector) && !(A is IMyShipMergeBlock) 
		&& !(A is IMyTextPanel) && !(A is IMyUpgradeModule) && !(A is IMyTextSurface)
		&& !(A is IMyProgrammableBlock) && !(A is IMyTimerBlock))
        {
            A.Enabled = true;
        }
    }
}

public void Normal(){
    setProduction(true);
    setH2(true);
    setBatteries(true);
    setReactors(false);
}

public void Combat(){
    setProduction(false);
    setH2(true);
    setBatteries(true);
}

public void Auto(){
    lastUpCounter++;
    lastDropCounter++;
	IMyTextPanel ControlScreen = GridTerminalSystem.GetBlockWithName("#EnergyControl") as IMyTextPanel;
    	if((getOutputPercent() > 90f || getPowerPercent() < 10f)&& lastUpCounter>20){
            lastUpCounter = 0;
            if(getBatteries() == false){ // Emergency Level 0
                setBatteries(true);
                enableBatteries(true);
                		ControlScreen.WriteText("\nBatteries turned on", true);
		    }
            else
            if(getH2() == false){ // Emergency Level 1
            	setH2(true);
            		ControlScreen.WriteText("\nPower cores turned on", true);
            }
            else
            if(getProduction() == true){ // Emergency Level 2
            	setProduction(false);
            		ControlScreen.WriteText("\nProduction blocks\nturned off", true);
            }
		    else
		    if(getReactors() == false){ // Emergency Level 3
		    	setReactors(true);
                	ControlScreen.WriteText("\nReactors\nturned on", true);
		    }
            else // Emergency Level 4
            if(checkIfEmergency()) Emergency();
    	}
    	else
    	if(getOutputPercent() < 10f && getPowerPercent() > 10f && lastDropCounter>100){
            lastDropCounter = 0;
        	if(getReactors()==true){
            	setReactors(false);
            	ControlScreen.WriteText("\nReactors turned off", true);
       		}
        	else
        	if(getProduction()==false){
            	setProduction(true);
            	ControlScreen.WriteText("\nProduction turned\nback online", true);
        	}
        	else
        	if(getH2()==true && getBatteries()==true){
            	setH2(false);
            	ControlScreen.WriteText("\nPower Cores\nare offline", true);
            }
        	else{
            	if(getOutputPercent() < (float)3 && getMedH2Capacity() > 0.85D){
                	setH2(true);
                	setBatteries(false);
                	ControlScreen.WriteText("\nRecharging Batteries", true);
            	}
        	}
    	}
}

public void Output(String output){
    foreach(IMyTextPanel EnergyScreen in getEnergyScreen()){
        EnergyScreen.FontSize = 2.0f;
        EnergyScreen.WriteText(output, false);
    }
}

public List<IMyTextPanel> getEnergyScreen(){
	List<IMyTextPanel> output = new List<IMyTextPanel>();
	List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
	GridTerminalSystem.SearchBlocksOfName("#EnergyControl", temp);
	
	foreach(IMyTerminalBlock b in temp){
		if(b is IMyTextPanel  && isOnThisGrid(b.CubeGrid)) {
    		IMyTextPanel tempo = b as IMyTextPanel;
    		output.Add(tempo);
		}
	}
	temp.Clear();
	
	return output;
}

public void Main(string argument, UpdateType updateSource) {
    Echo(argument+" "+LastState);
    if(LastState=="NULL") LastState="NORMAL";
    switch(argument){
        case "AUTO":
            LastState="AUTO";
    		Output("\n AUTO MODE");
            Auto();
        break;

        case "NORMAL":
            LastState="NORMAL";
            Output("\n\n\nNORMAL MODE");
            Normal();
        break;

        case "COMBAT":
            LastState="COMBAT";
            Output("\n\n\nCOMBAT MODE");
            Combat();
        break;

        default:
        if(LastState == "AUTO"){
    		Output("\n AUTO MODE");
            Auto();
        }
        else if(LastState == "EMERGENCY" && !checkIfEmergency()) {ClearEmergency(); LastState = "AUTO";}
        break;
    }
}
