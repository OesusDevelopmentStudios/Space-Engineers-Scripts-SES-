      ///////////////////
     /// VERSION 0.1 ///
    ///////////////////

  /////////////////////////
 /// UTILITY FUNCTIONS ///
/////////////////////////

public List<IMyAirtightHangarDoor> getDoors(){
	List<IMyAirtightHangarDoor> Doors = new List<IMyAirtightHangarDoor>();
	List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
	GridTerminalSystem.SearchBlocksOfName("Hatch", temp);
	
	foreach(IMyTerminalBlock b in temp){
		if(b is IMyAirtightHangarDoor) {
    		IMyAirtightHangarDoor tempo = b as IMyAirtightHangarDoor;
    		Doors.Add(tempo);
		}
	}
	temp.Clear();
	
	return Doors;
}

public List<IMyFunctionalBlock> getBeltBlocks(){
	List<IMyFunctionalBlock> blocks = new List<IMyFunctionalBlock>();
	List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
	GridTerminalSystem.SearchBlocksOfName("[BELT]", temp);
	
	foreach(IMyTerminalBlock b in temp){
		if(b is IMyFunctionalBlock) {
    		IMyFunctionalBlock tempo = b as IMyFunctionalBlock;
    		blocks.Add(tempo);
		}
	}
	temp.Clear();
	
	return blocks;
}

public List<IMyShipMergeBlock> getMergers(){
	List<IMyShipMergeBlock> Mergers = new List<IMyShipMergeBlock>();
	List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
	GridTerminalSystem.SearchBlocksOfName("Merger", temp);
	
	foreach(IMyTerminalBlock b in temp){
		if(b is IMyShipMergeBlock) {
    		IMyShipMergeBlock tempo = b as IMyShipMergeBlock;
    		Mergers.Add(tempo);
		}
	}
	temp.Clear();
	
	return Mergers;
}

public List<IMyGravityGenerator> getAccelerators(){
	List<IMyGravityGenerator> Accelerators = new List<IMyGravityGenerator>();
	List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
	GridTerminalSystem.SearchBlocksOfName("Accelerator", temp);
	
	foreach(IMyTerminalBlock b in temp){
		if(b is IMyGravityGenerator) {
    		IMyGravityGenerator tempo = b as IMyGravityGenerator;
    		Accelerators.Add(tempo);
		}
	}
	temp.Clear();
	
	return Accelerators;
}

public List<IMyShipWelder> getConstructors(){
	List<IMyShipWelder> Construtors = new List<IMyShipWelder>();
	List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
	GridTerminalSystem.SearchBlocksOfName("Constructor", temp);
	
	foreach(IMyTerminalBlock b in temp){
		if(b is IMyShipWelder) {
    		IMyShipWelder tempo = b as IMyShipWelder;
    		Construtors.Add(tempo);
		}
	}
	temp.Clear();
	
	return Construtors;
}

public bool SafeToFire(){
	List<IMyAirtightHangarDoor> Doors = getDoors();
	
	
	foreach(IMyAirtightHangarDoor A in Doors){
		if(A.OpenRatio<0.8f) return false;
	}
	return true;
}

public void setDoors(bool open)
{
    List<IMyAirtightHangarDoor> Doors = getDoors();
    if (open) foreach (IMyAirtightHangarDoor Abip in Doors) Abip.OpenDoor();
    else foreach (IMyAirtightHangarDoor Abip in Doors) Abip.CloseDoor();
}

public void toggleDoors()
{
    List<IMyAirtightHangarDoor> Doors = getDoors();
    foreach (IMyAirtightHangarDoor Abip in Doors)
    {
        if (Abip.IsWorking && (Abip.Status == DoorStatus.Closed || Abip.Status == DoorStatus.Closing)) { setDoors(true); }
        else if (Abip.IsWorking && (Abip.Status == DoorStatus.Open || Abip.Status == DoorStatus.Opening)) { setDoors(false); }
        else continue;
    }
}

public String doorStatus(){
	List<IMyAirtightHangarDoor> Doors = getDoors();
	
	float CurrRatio = 44f;
	DoorStatus CurrStatus = DoorStatus.Closed;
	
	foreach(IMyAirtightHangarDoor A in Doors){
		if(A.IsFunctional){
			CurrRatio = A.OpenRatio;
			CurrStatus = A.Status;
			break;
		}
	}
	
	if(CurrRatio>1f) return "All hatches are damaged.";
	else{
		switch(CurrStatus){
			case DoorStatus.Open: 		return "Hatches Open";
			case DoorStatus.Opening: 	return "Hatches at " + (CurrRatio*100f).ToString("0.") +"% and opening";
			case DoorStatus.Closed:		return "Hatches Closed";
			case DoorStatus.Closing:	return "Hatches at " + (CurrRatio*100f).ToString("0.") +"% and closing";
			
			default: return "Fuck you in your sloppy anus you fucking bitch.";
		}
	}
}

public void DefaultLook(){
	IMyTextPanel ControlScreen	= GridTerminalSystem.GetBlockWithName("#ArbalestControl") 	as IMyTextPanel;
	ControlScreen.WriteText("");
	ControlScreen.FontSize = 1.0f;
	ControlScreen.BackgroundColor = Color.Black;	
}

public String BeltStatus(){
	List<IMyFunctionalBlock> blocks = getBeltBlocks();
	
	int mass=0, batteries=0, mergers=0;
	
	foreach(IMyFunctionalBlock B in blocks){
		if(B is IMyBatteryBlock)		batteries++;
		else if(B is IMyArtificialMassBlock)	mass++;
		else if(B is IMyShipMergeBlock)		mergers++;		
	}
	
	if(mass==0||batteries==0||mergers==0) 		return "No missiles ready.";
	else if(mass==1||batteries==1||mergers==1)	return "1 missile loaded.";
	else if(mass==2||batteries==2||mergers==2)	return "2 missiles loaded.";
	else if(mass==3||batteries==3||mergers==3)	return "3 missiles loaded.";
	else 						return "All missiles loaded.";	
}

public void Output(String output){
	IMyTextPanel ControlScreen	= GridTerminalSystem.GetBlockWithName("#ArbalestControl") 	as IMyTextPanel;
	ControlScreen.WriteText(output, true);
}

public void AlertOutput(String output){
	IMyTextPanel ControlScreen	= GridTerminalSystem.GetBlockWithName("#ArbalestControl") 	as IMyTextPanel;
	ControlScreen.FontSize = 2.0f;
	ControlScreen.BackgroundColor = Color.DarkRed;
	ControlScreen.WriteText(output, false);
}

public void AccelControl(String Argument, bool Standby){
	List<IMyGravityGenerator> Accelerators = getAccelerators();
	switch(Argument){
		case "all":
			if(!Standby){
				foreach(IMyGravityGenerator g in Accelerators){
					g.FieldSize = new Vector3(14f,150f,14f);
					g.GravityAcceleration = -10f;
				}
				Output("All generators turned on.");
			}
			else{
				foreach(IMyGravityGenerator g in Accelerators){
					g.FieldSize = new Vector3(1f,1f,1f);
					g.GravityAcceleration = 0f;
				}
				Output("All generators on standby.");				
			}	
			break;
		
		case "LU":
			if(!Standby){
				foreach(IMyGravityGenerator g in Accelerators){
					if(g.CustomName == "Left Up Accelerator"){
						g.FieldSize = new Vector3(14f,150f,14f);
						g.GravityAcceleration = -10f;
					}
				}
				Output("Upper left generators turned on.");
			}
			else{
				foreach(IMyGravityGenerator g in Accelerators){
					if(g.CustomName == "Left Up Accelerator"){
						g.FieldSize = new Vector3(1f,1f,1f);
						g.GravityAcceleration = 0f;
					}
				}
				Output("Upper left generators on standby.");				
			}
			break;
		
		case "LD":
			if(!Standby){
				foreach(IMyGravityGenerator g in Accelerators){
					if(g.CustomName == "Left Down Accelerator"){
						g.FieldSize = new Vector3(14f,150f,14f);
						g.GravityAcceleration = -10f;
					}
				}
				Output("Bottom left generators turned on.");
			}
			else{
				foreach(IMyGravityGenerator g in Accelerators){
					if(g.CustomName == "Left Down Accelerator"){
						g.FieldSize = new Vector3(1f,1f,1f);
						g.GravityAcceleration = 0f;
					}
				}
				Output("Bottom left generators on standby.");				
			}
			break;
		
		case "RU":
			if(!Standby){
				foreach(IMyGravityGenerator g in Accelerators){
					if(g.CustomName == "Right Up Accelerator"){
						g.FieldSize = new Vector3(14f,150f,14f);
						g.GravityAcceleration = -10f;
					}
				}
				Output("Upper right generators turned on.");
			}
			else{
				foreach(IMyGravityGenerator g in Accelerators){
					if(g.CustomName == "Right Up Accelerator"){
						g.FieldSize = new Vector3(1f,1f,1f);
						g.GravityAcceleration = 0f;
					}
				}
				Output("Upper right generators on standby.");				
			}
			break;
		
		case "RD":
			if(!Standby){
				foreach(IMyGravityGenerator g in Accelerators){
					if(g.CustomName == "Right Down Accelerator"){
						g.FieldSize = new Vector3(14f,150f,14f);
						g.GravityAcceleration = -10f;
					}
				}
				Output("Bottom right generators turned on.");
			}
			else{
				foreach(IMyGravityGenerator g in Accelerators){
					if(g.CustomName == "Right Down Accelerator"){
						g.FieldSize = new Vector3(1f,1f,1f);
						g.GravityAcceleration = 0f;
					}
				}
				Output("Bottom right generators on standby.");				
			}
			break;
		
		default:
			AlertOutput("\n\nINVALID\nCOMMAND");
			break;
	}
}


  //////////////////////
 /// MAIN FUNCTIONS ///
//////////////////////


 public void WriteStatus(){
	List<IMyAirtightHangarDoor> Doors 		= getDoors();
	List<IMyShipMergeBlock> 	Mergers 	= getMergers();
	List<IMyGravityGenerator> 	Accelerators 	= getAccelerators();
	List<IMyShipWelder> 		Construtors 	= getConstructors();
    int mconnected = 0, monline = 0;

    IMyTextPanel ControlScreen = GridTerminalSystem.GetBlockWithName("#ArbalestControl") as IMyTextPanel;
    Output(BeltStatus() + "\n\n");
    Output(doorStatus() + "\n");
    if (Doors.Count < 8) Output(Doors.Count.ToString() + "/8 Safety Hatches present.\n");
    foreach (IMyShipMergeBlock m in Mergers)
    {
        if (m.IsFunctional)
        {
            monline++;
            if (m.IsConnected) mconnected++;
        }
    }
    Output(monline.ToString() + "/4 Launch Platforms present,\nof which " + mconnected.ToString() + " are connected.\n");
    if (Accelerators.Count < 12) Output(Accelerators.Count.ToString() + "/12 Belt Accelerators present.\n");
    if (Construtors.Count < 4) Output(Construtors.Count.ToString() + "/4 Belt Constructors present.\n");
}

public void VolleyFire(){
	/*
	Alpha - RU
	Beta  - LU
	Gamma - LD	
	Delta - RD	 
	*/
	
	IMyTimerBlock TAlpha	= GridTerminalSystem.GetBlockWithName("#Arbalest Timer Alpha") as IMyTimerBlock;
	IMyTimerBlock TBeta	= GridTerminalSystem.GetBlockWithName("#Arbalest Timer Beta" ) as IMyTimerBlock;
	IMyTimerBlock TGamma	= GridTerminalSystem.GetBlockWithName("#Arbalest Timer Gamma") as IMyTimerBlock;
	IMyTimerBlock TDelta	= GridTerminalSystem.GetBlockWithName("#Arbalest Timer Delta") as IMyTimerBlock;	
	
	if(!SafeToFire()){
		AlertOutput("\n\nFIRING UNSAFE\nUNABLE TO COMPLY.");
	}
	else{
		if(BeltStatus() != "No missiles ready." && !(TDelta.IsCountingDown)){
			List<IMySoundBlock> Sounds = new List<IMySoundBlock>();
    			GridTerminalSystem.GetBlocksOfType<IMySoundBlock> (Sounds);

            		TAlpha. TriggerDelay = 1.0f; TAlpha.StartCountdown();
           		TBeta.  TriggerDelay = 3.5f; TBeta.StartCountdown();
            		TGamma. TriggerDelay = 6.0f; TGamma.StartCountdown();
            		TDelta. TriggerDelay = 8.5f; TDelta.StartCountdown();
       		}
		else 	AlertOutput("\n\nSTAND BY\nFIRING IN\nPROGRESS");
	}
}

public void CeaseFire(){
	IMyTimerBlock TAlpha	= GridTerminalSystem.GetBlockWithName("#Arbalest Timer Alpha"	) as IMyTimerBlock;
	IMyTimerBlock TBeta	= GridTerminalSystem.GetBlockWithName("#Arbalest Timer Beta" 	) as IMyTimerBlock;
	IMyTimerBlock TGamma	= GridTerminalSystem.GetBlockWithName("#Arbalest Timer Gamma"	) as IMyTimerBlock;
	IMyTimerBlock TDelta	= GridTerminalSystem.GetBlockWithName("#Arbalest Timer Delta"	) as IMyTimerBlock;
	IMyTimerBlock Cleanup	= GridTerminalSystem.GetBlockWithName("#Arbalest Cleanup Timer"	) as IMyTimerBlock;
	
	TAlpha	.StopCountdown();
	TBeta	.StopCountdown();
	TGamma	.StopCountdown();
	TDelta	.StopCountdown();
	Cleanup .Trigger();
			
	AlertOutput("\n\nCEASING FIRE"); 
}


public void Main(string argument, UpdateType updateSource) {
	DefaultLook();
	
	switch(argument)
	{
		case "status": 
			WriteStatus();
			break;
		
		case "fire":
			try{
			VolleyFire();
			}
			catch(Exception e){
				Output("TIMER BLOCK(S) DAMAGED OR NOT EXISTING:\n");
				Output(e.Message);   
			}
			break;
		
		case "stop":
			try{
			CeaseFire();
			}
			catch(Exception e){
				Output("TIMER BLOCK(S) DAMAGED OR NOT EXISTING:\n");
				Output(e.Message);    
			}
			break;
		
		case "LUN":
			AccelControl("LU",false);
			break;
		
		case "LUF":
			AccelControl("LU",true);
			break;
		
		case "LDN":
			AccelControl("LD",false);
			break;
		
		case "LDF":
			AccelControl("LD",true);
			break;
		
		case "RUN":
			AccelControl("RU",false);
			break;
		
		case "RUF":
			AccelControl("RU",true);
			break;
		
		case "RDN":
			AccelControl("RD",false);
			break;
		
		case "RDF":
			AccelControl("RD",true);
			break;
		
		case "AF":
			AccelControl("all", true);
			break;
			
		case "HO":
			setDoors(true);
			break;
			
		case "HC":
			setDoors(false);
			break;
			
		default: AlertOutput("UNKNOWN COMMAND"); 
			 break;  	
	}
}
