// UTILITY

//Donnager/Sound Block

public List<IMyLightingBlock> getAlarmLights(){
    return getLights("Donnager/Alarm Light");
}

public List<IMyLightingBlock> getMoodLights(){
    return getLights("Donnager/Mood Light");
}

public void closeAllDoors(){
    
}

public List<IMyLightingBlock> getOtherLights(){
    return getLights("Donnager/Other Light");
}

public IMyTimerBlock getSpecificTimer(string name){
    IMyTimerBlock result = GridTerminalSystem.GetBlockWithName(name) as IMyTimerBlock;
    return result;
}

public List<IMyLightingBlock> getLights(string name){
    List<IMyLightingBlock> result = new List<IMyLightingBlock>();
    List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
    GridTerminalSystem.SearchBlocksOfName(name, temp);

    foreach (IMyTerminalBlock b in temp)
    {
        if (b is IMyLightingBlock)
        {
            IMyLightingBlock tempo = b as IMyLightingBlock;
            result.Add(tempo);
        }
    }
    temp.Clear();

    return result;
}

public void changeLightsStatus(List<IMyLightingBlock> target, bool turnon){
    foreach (IMyLightingBlock LB in target)
    {
        LB.Enabled = turnon;
    }
}

public void changeLightsColor(List<IMyLightingBlock> target, Color color){
    foreach (IMyLightingBlock LB in target)
    {
        LB.Color = color;
    }
}

public IMyTextPanel getSupportScreen(){
    return GridTerminalSystem.GetBlockWithName("#SupportScreen") as IMyTextPanel;
}

public void output(string input){
    IMyTextPanel screen = getSupportScreen();
    screen.FontSize = (float)1.9;
    screen.WriteText(input,false);
}

//Main Functions

public void switchRedAlert(){
    IMyTimerBlock target = getSpecificTimer("#RedAlertTimer");
    List<IMyLightingBlock> moodLights = getMoodLights();
    List<IMyLightingBlock> alarmLights = getAlarmLights();
    List<IMyLightingBlock> otherLights = getOtherLights();
    IMySoundBlock alarmBlock = GridTerminalSystem.GetBlockWithName("Donnager/Sound Block") as IMySoundBlock;
    
    IMyProgrammableBlock controlBlock = GridTerminalSystem.GetBlockWithName("Energy Control Block") as IMyProgrammableBlock;

    if(target == null || !target.Enabled)
    {   // turn the <s> Fucking Furries </s> on
        changeLightsColor(moodLights, Color.Red);
        changeLightsColor(otherLights, new Color(40,0,0));
        changeLightsStatus(alarmLights, true);
        target.Enabled = true;
        alarmBlock.Play();
        foreach(IMyLightingBlock l in moodLights)
        {
            l.BlinkIntervalSeconds = 2;
        }
        target.StartCountdown();
        output("\n\n\nRED ALERT");
        if(controlBlock!=null) controlBlock.TryRun("COMBAT");
    }
    else 
    {   // turn the <s> Fucking Furries </s> off
        changeLightsColor(moodLights, Color.Yellow);
        changeLightsColor(otherLights, new Color(255,255,255));
        changeLightsStatus(alarmLights, false);
        target.Enabled = false;
        foreach(IMyLightingBlock l in moodLights)
        {
            l.BlinkIntervalSeconds = 0;
        }
        output("\n\n\nIN ORDER");
        if(controlBlock!=null) controlBlock.TryRun("NORMAL");
    }

}

public void Main(string argument, UpdateType updateSource)
{
    switchRedAlert();
}