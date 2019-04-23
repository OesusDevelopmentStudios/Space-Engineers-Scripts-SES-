public String toPercent(float up, float down){
    float input = (100*up)/down;
    String output = input.ToString("0.00");
    return output;
}

public String printEnergyInfo(){
        float ShipsStoredPower 	= 0;
        float ShipsMaxPower    	= 0;
        float MaxShipOutput    	= 0;
        float CurrentShipOutput	= 0;
        int   Online			= 0;
        int   Recharging 		= 0;
        int   Empty 			= 0;
        int   Offline 			= 0;
        int   RNominal	 		= 0;
        int   ROff 				= 0;
        
        List<IMyPowerProducer>   Producers  = new List<IMyPowerProducer>();
        GridTerminalSystem.GetBlocksOfType<IMyPowerProducer> (Producers);
        
        foreach (IMyPowerProducer P in Producers){
            if(P.IsWorking) MaxShipOutput 	+= P.MaxOutput;
            CurrentShipOutput  				+= P.CurrentOutput;
            
	if(P is IMySolarPanel) {}
        else if(!(P is IMyBatteryBlock)){
                if(P.IsWorking) RNominal++;
        	else 			ROff++;
        }
        	else{
				IMyBatteryBlock B = (IMyBatteryBlock) P;
				
            	ShipsStoredPower  += B.CurrentStoredPower;
            	ShipsMaxPower     += B.MaxStoredPower;
            	CurrentShipOutput -= B.CurrentInput;
            
            	if(B.CurrentStoredPower==0) Empty++;
            	else if(!(B.IsWorking)) 	Offline++;
            	else if(B.ChargeMode == ChargeMode.Recharge) Recharging++;
            	else 						Online++;
			}
        }
        
        float convert = ((float)10/(float)36);
        
        CurrentShipOutput 	= convert * CurrentShipOutput;
        MaxShipOutput		= convert * MaxShipOutput;
        
        String output =                   	  " Current Power: " + ShipsStoredPower*1000 	+ "/" + ShipsMaxPower*1000 + " kW (" + 
        toPercent(ShipsStoredPower,ShipsMaxPower) + "%)";
        output +=                       	"\n Current Output: "	+ CurrentShipOutput.ToString("0.00") 	+ "/" + MaxShipOutput.ToString("0.0")  + 
        " kWs (" + toPercent(CurrentShipOutput,MaxShipOutput) + "%)";
        if(RNominal>0||ROff>0) output +=	"\n Cores Online:    " 	+ RNominal 					+ "/" + (RNominal+ROff);
        	else output +=             		"\n No power cores present!";
        	
        output +=                           "\n Batteries:          "	+ Online + "/" + (Online+Empty+Recharging+Offline) + " Online";
        if(Recharging>0) output +=   		"\n                           "	+ Recharging + " Recharging";
        if(Empty>0) output += 	          	"\n                           "	+ Empty + " Empty";

        return output;
}


public void Main(string argument, UpdateType updateSource) {
        IMyTextPanel EnergyScreen = GridTerminalSystem.GetBlockWithName("#EnergyScreen") as IMyTextPanel;
        EnergyScreen.FontSize = (float)1.9;
        EnergyScreen.WriteText(printEnergyInfo(), false);
}
