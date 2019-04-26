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
        foreach(IMyProductionBlock P in Producers){
            P.Enabled = set;
        }
}

public bool getProduction (){
        List<IMyProductionBlock>   Producers  = new List<IMyProductionBlock>();
        GridTerminalSystem.GetBlocksOfType<IMyProductionBlock> (Producers);
        foreach(IMyProductionBlock P in Producers){
            if(P.Enabled==true) return true;
        }
        return false;
}

public void setH2 (bool set){
        List<IMyPowerProducer>   Producers  = new List<IMyPowerProducer>();
        GridTerminalSystem.GetBlocksOfType<IMyPowerProducer> (Producers);
        foreach(IMyPowerProducer P in Producers){
            if(!(P is IMyReactor)&&!(P is IMyBatteryBlock)) P.Enabled = set;
        }
}

public bool getH2 (){
        List<IMyPowerProducer>   Producers  = new List<IMyPowerProducer>();
        GridTerminalSystem.GetBlocksOfType<IMyPowerProducer> (Producers);
        foreach(IMyPowerProducer P in Producers){
            if(!(P is IMyReactor)&&!(P is IMyBatteryBlock)) if(P.Enabled==false) return false;
        }
        return true;
}

public void setReactors (bool set){
        List<IMyReactor>   Reactors  = new List<IMyReactor>();
        GridTerminalSystem.GetBlocksOfType<IMyReactor> (Reactors);
        foreach(IMyReactor P in Reactors){
            P.Enabled = set;
        }
}

public bool getReactors (){
        List<IMyReactor>   Reactors  = new List<IMyReactor>();
        GridTerminalSystem.GetBlocksOfType<IMyReactor> (Reactors);
        foreach(IMyReactor P in Reactors){
            if(P.Enabled == false) return false;
        }
        return true;
}

public void setBatteries (bool set){
        List<IMyBatteryBlock>   Batteries  = new List<IMyBatteryBlock>();
        GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock> (Batteries);
        foreach(IMyBatteryBlock B in Batteries){
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

public void Emergency(){
    setReactors(true);
    List<IMyFunctionalBlock> Everything = new List<IMyFunctionalBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyFunctionalBlock> (Everything);
    foreach(IMyFunctionalBlock A in Everything){
        if(!(A is IMyReactor) && !(A is IMyShipConnector) && !(A is IMyShipMergeBlock) 
		&& !(A is IMyTextPanel) && !(A is IMyUpgradeModule) && !(A is IMyTextSurface)
		&& !(A is IMyProgrammableBlock) && !(A is IMyTimerBlock))
        {
            A.Enabled = false;
        }
    }
}

public void Recharge(){
    setProduction(false);
    setH2(true);
    setBatteries(false);
    setReactors(false);
    
    if(getPowerPercent()<=1) Emergency();
    if(getPowerPercent()>80) {
        	IMyTimerBlock bstdby = GridTerminalSystem.GetBlockWithName("#Recharge Timer") 	as IMyTimerBlock;
    	   	bstdby.Trigger();
    	}	
}

public void Standby(){
	
    setProduction(false);
    setH2(false);
    setBatteries(true);
    enableBatteries(true);
    setReactors(false);
    
    if(getPowerPercent()<=5) {
    	IMyTimerBlock brecharge = GridTerminalSystem.GetBlockWithName("#Recharge Timer") 	as IMyTimerBlock;
		brecharge.Trigger();	
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
	IMyTextPanel ControlScreen = GridTerminalSystem.GetBlockWithName("#EnergyControl") as IMyTextPanel;
    if(getOutputPercent() > (float)90 || getPowerPercent() < (float)10){
        if(getBatteries() == false){
            setBatteries(true);
            enableBatteries(true);
            ControlScreen.WriteText("\nBatteries turned on", true);
        }
        else
        if(getH2() == false){
            setH2(true);
            ControlScreen.WriteText("\nPower cores turned on", true);
        }
        else
        if(getProduction() == true){
            setProduction(false);
            ControlScreen.WriteText("\nProduction blocks\nturned off", true);
        }
    }
    else
    if(getOutputPercent() < (float)10){
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
            if(getOutputPercent() < (float)3){
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
		if(b is IMyTextPanel) {
    		IMyTextPanel tempo = b as IMyTextPanel;
    		output.Add(tempo);
		}
	}
	temp.Clear();
	
	return output;
}


public void Main(string argument, UpdateType updateSource) {
		
		bool norm = true, stdby = true, comb = true, aut = true, rech = true;
		
		try{
			IMyTimerBlock bnormal		= GridTerminalSystem.GetBlockWithName("#Normal Timer")		as IMyTimerBlock;
			bnormal.StopCountdown();
		}
		catch (Exception e){
			Output("NORMAL MODE TIMER\nNOT PRESENT\n");
			norm = false;
		}
		try{
			IMyTimerBlock bstdby		= GridTerminalSystem.GetBlockWithName("#Standby Timer") 	as IMyTimerBlock;
			bstdby.StopCountdown();
		}
		catch (Exception e){
			Output("STANDBY MODE TIMER\nNOT PRESENT\n");
			stdby = false;
		}
		try{
			IMyTimerBlock bcombat		= GridTerminalSystem.GetBlockWithName("#Combat Timer")		as IMyTimerBlock;
			bcombat.StopCountdown();
		}
		catch (Exception e){
			Output("COMBAT MODE TIMER\nNOT PRESENT\n");
			comb = false;
		}
		try{
			IMyTimerBlock bauto			= GridTerminalSystem.GetBlockWithName("#Auto Timer") 		as IMyTimerBlock;
			bauto.StopCountdown();
		}
		catch (Exception e){
			Output("AUTO MODE TIMER\nNOT PRESENT\n");
			aut = false;
		}
		try{
			IMyTimerBlock brecharge		= GridTerminalSystem.GetBlockWithName("#Recharge Timer") 	as IMyTimerBlock;
			brecharge.StopCountdown();
		}
		catch (Exception e){
			Output("RECHARGE MODE TIMER\nNOT PRESENT\n");
			rech = false;
		}      
        switch(argument){
            case "auto":
 			if(aut==true){
        		Output("\n AUTO MODE");
        		IMyTimerBlock kwi		= GridTerminalSystem.GetBlockWithName("#Auto Timer")		as IMyTimerBlock;
        		kwi.StartCountdown();
                Auto();
			}
            break;

            case "standby":
 			if(stdby==true){
                Output("\n\n\n\nSTANDING BY");
        		IMyTimerBlock kwi		= GridTerminalSystem.GetBlockWithName("#Standby Timer")		as IMyTimerBlock;
        		kwi.StartCountdown();                
                Standby();
			 }
            break;

            case "normal":
 			if(norm==true){
                Output("\n\n\n\nNORMAL MODE");
        		IMyTimerBlock kwi		= GridTerminalSystem.GetBlockWithName("#Normal Timer")		as IMyTimerBlock;
        		kwi.StartCountdown();
                Normal();
			 }
            break;

            case "combat":
 			if(comb==true){
                Output("\n\n\n\nCOMBAT MODE");
        		IMyTimerBlock kwi		= GridTerminalSystem.GetBlockWithName("#Combat Timer")		as IMyTimerBlock;
        		kwi.StartCountdown();
                Combat();
			 }
            break;
			
			case "recharge":
 			if(rech==true){
                Output("\n\n\n\nRECHARGING");
        		IMyTimerBlock kwi		= GridTerminalSystem.GetBlockWithName("#Recharge Timer")	as IMyTimerBlock;
        		kwi.StartCountdown();
                Combat();
			 }
            break;
		
            case "EmergencyTest":
                Emergency();
            break;
        }
}
