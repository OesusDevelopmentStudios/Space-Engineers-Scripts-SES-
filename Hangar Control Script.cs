/// UTILITY FUNCTIONS
/// 
public IMyTextPanel getScreen()
{
    IMyTextPanel ControlScreen = GridTerminalSystem.GetBlockWithName("#HangarControl") as IMyTextPanel;
    return ControlScreen;
}

public List<IMyAirtightHangarDoor> getInternalDoors()
{
    /*
        Drzwi, które równoczeœnie umo¿liwiaj¹ transport pomiêdzy lewym i prawym hangarem oraz
        umo¿liwiaj¹ utrzymanie jednego hangaru pod ciœnieniem, a drugiego przeciwnie.
     */
    List<IMyAirtightHangarDoor> Doors = new List<IMyAirtightHangarDoor>();
    List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
    GridTerminalSystem.SearchBlocksOfName("[]", temp); //TODO: ogarniêcie  zeby ta nazwa aktualnie mia³a jakiœ sens

    foreach (IMyTerminalBlock b in temp)
    {
        if (b is IMyAirtightHangarDoor)
        {
            IMyAirtightHangarDoor tempo = b as IMyAirtightHangarDoor;
            Doors.Add(tempo);
        }
    }
    temp.Clear();

    return Doors;
}

public List<IMyDoor> getLeftEntranceDoors()
{
    /*
        Drzwi prowadz¹ce z hangaru do reszty okrêtu. Podczas zmiany stanu lewych drzwi hangaru na "Open",
        powinny zostaæ zamkniête, by nie zdepressurize'owaæ ca³oœci okrêtu.
     */
    List<IMyDoor> Doors = new List<IMyDoor>();
    List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
    GridTerminalSystem.SearchBlocksOfName("[]", temp); //TODO: ogarniêcie  zeby ta nazwa aktualnie mia³a jakiœ sens

    foreach (IMyTerminalBlock b in temp)
    {
        if (b is IMyDoor)
        {
            IMyDoor tempo = b as IMyDoor;
            Doors.Add(tempo);
        }
    }
    temp.Clear();

    return Doors;
}

public List<IMyDoor> getRightEntranceDoors()
{
    /*
        Drzwi prowadz¹ce z hangaru do reszty okrêtu. Podczas zmiany stanu prawych drzwi hangaru na "Open",
        powinny zostaæ zamkniête, by nie zdepressurize'owaæ ca³oœci okrêtu.
     */
    List<IMyDoor> Doors = new List<IMyDoor>();
    List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
    GridTerminalSystem.SearchBlocksOfName("[]", temp); //TODO: ogarniêcie  zeby ta nazwa aktualnie mia³a jakiœ sens

    foreach (IMyTerminalBlock b in temp)
    {
        if (b is IMyDoor)
        {
            IMyDoor tempo = b as IMyDoor;
            Doors.Add(tempo);
        }
    }
    temp.Clear();

    return Doors;
}

public List<IMyAirVent> getMainOxygenOutlets()
{
    /*
        Outlety z g³ównej rezerwy tlenu na okrêcie, powinny w³¹czyæ siê jako drugie, po
        tym, gdy hangarowe pojemniki z tlenem wypuszcz¹ ju¿ absolutnie wszystko. 
     */
    List<IMyAirVent> output = new List<IMyAirVent>();
    List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
    GridTerminalSystem.SearchBlocksOfName("[]", temp); //TODO: ogarniêcie  zeby ta nazwa aktualnie mia³a jakiœ sens

    foreach (IMyTerminalBlock b in temp)
    {
        if (b is IMyAirVent)
        {
            IMyAirVent tempo = b as IMyAirVent;
            output.Add(tempo);
        }
    }
    temp.Clear();

    return output;
}

public List<IMyAirVent> getReserveOxygenOutlets()
{
    /*
        Outlety ze specjalnej, hangarowej rezerwy tlenu, maj¹ jako pierwsze jak najszybciej wype³niæ hangary
        tlenem
     */
    List<IMyAirVent> output = new List<IMyAirVent>();
    List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
    GridTerminalSystem.SearchBlocksOfName("[]", temp); //TODO: ogarniêcie  zeby ta nazwa aktualnie mia³a jakiœ sens

    foreach (IMyTerminalBlock b in temp)
    {
        if (b is IMyAirVent)
        {
            IMyAirVent tempo = b as IMyAirVent;
            output.Add(tempo);
        }
    }
    temp.Clear();

    return output;
}

public List<IMyGasTank> getOxygenReserve()
{
    /*
        Zbiorniki z tlenem umieszczone pod mostkiem, od³¹czone od reszty systemu logistycznego okrêtu.
        Stworzone w ten sposób, ¿eby uproœciæ algorytm/skrypt do minimum.
        Nie powinny posiadaæ ¿adnego dodatkowego systemu tworzenia tlenu, bo ich przeznaczeniem jest 
        byæ pustymi przez wiêkszoœæ czasu
     */
    List<IMyGasTank> output = new List<IMyGasTank>();
    List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
    GridTerminalSystem.SearchBlocksOfName("[]", temp); //TODO: ogarniêcie  zeby ta nazwa aktualnie mia³a jakiœ sens

    foreach (IMyTerminalBlock b in temp)
    {
        if (b is IMyGasTank)
        {
            IMyGasTank tempo = b as IMyGasTank;
            output.Add(tempo);
        }
    }
    temp.Clear();

    return output;
}


public List<IMyAirtightHangarDoor> getLeftHangar()
{
    List<IMyAirtightHangarDoor> Doors = new List<IMyAirtightHangarDoor>();
    List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
    GridTerminalSystem.SearchBlocksOfName("Left Hangar Block", temp);

    foreach (IMyTerminalBlock b in temp)
    {
        if (b is IMyAirtightHangarDoor)
        {
            IMyAirtightHangarDoor tempo = b as IMyAirtightHangarDoor;
            Doors.Add(tempo);
        }
    }
    temp.Clear();

    return Doors;
}

public List<IMyAirtightHangarDoor> getRightHangar()
{
    List<IMyAirtightHangarDoor> Doors = new List<IMyAirtightHangarDoor>();
    List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
    GridTerminalSystem.SearchBlocksOfName("Right Hangar Block", temp);

    foreach (IMyTerminalBlock b in temp)
    {
        if (b is IMyAirtightHangarDoor)
        {
            IMyAirtightHangarDoor tempo = b as IMyAirtightHangarDoor;
            Doors.Add(tempo);
        }
    }
    temp.Clear();

    return Doors;
}

public DoorStatus HangarStatus(List<IMyAirtightHangarDoor> hangar)
{
    float CurrRatio = 44f;
    DoorStatus CurrStatus = DoorStatus.Closed;

    foreach (IMyAirtightHangarDoor A in hangar)
    {
        if (A.IsFunctional)
        {
            CurrRatio = A.OpenRatio;
            CurrStatus = A.Status;
            break;
        }
    }

    if (CurrRatio > 1f) return DoorStatus.Closed;
    else
    {
        switch (CurrStatus)
        {
            case DoorStatus.Opening: return DoorStatus.Opening;
            case DoorStatus.Closed: return DoorStatus.Closed;
            case DoorStatus.Closing: return DoorStatus.Closing;
            default: return DoorStatus.Open;
        }
    }
}

public String statusToString(DoorStatus input)
{
    if (input == DoorStatus.Closed) return "closed.";
    else if (input == DoorStatus.Closing) return "closing.";
    else if (input == DoorStatus.Opening) return "opening.";
    else return "open.";
}

public void DefaultLook()
{
    IMyTextPanel ControlScreen = getScreen();
    ControlScreen.WriteText("", false);
    ControlScreen.FontSize = 1.2f;
    ControlScreen.Alignment = TextAlignment.LEFT;
    ControlScreen.BackgroundColor = Color.Black;
}

public void Output(String output)
{
    IMyTextPanel ControlScreen = getScreen();
    ControlScreen.WriteText(output, true);
}

public void AlertOutput(String output)
{
    IMyTextPanel ControlScreen = getScreen();
    ControlScreen.FontSize = 2.0f;
    ControlScreen.Alignment = TextAlignment.CENTER;
    ControlScreen.BackgroundColor = Color.DarkRed;
    ControlScreen.WriteText(output);
}

/// MAIN FUNCTIONS

public void WriteStatus()
{
    IMyTextPanel ControlScreen = getScreen();
    Output("Left hangar doors are " + statusToString(HangarStatus(getLeftHangar())) +"\n");
    Output("Right hangar doors are " + statusToString(HangarStatus(getLeftHangar())) + "\n");
}

public void DoorControl(string argument, bool open)
{
    List<IMyAirtightHangarDoor> controledDoors;

    switch (argument)
    {
        case "LH":
            controledDoors = getLeftHangar();
            Output("\n\nLeft Hangar doors ");
            break;

        case "RH":
            controledDoors = getRightHangar();
            Output("\n\nLeft Hangar doors ");
            break;

        default:
            AlertOutput("\n\nWRONG ARGUMENT");
            return;
    }

    if (open)
    {
        foreach (IMyAirtightHangarDoor a in controledDoors)
        {
            a.OpenDoor();
        }
        Output("opening.");
    }
    else
    {
        foreach (IMyAirtightHangarDoor a in controledDoors)
        {
            a.CloseDoor();
        }
        Output("closing.");
    }
}

public void DoorControl(List<IMyAirtightHangarDoor> hangar, bool open)
{
    if (open)
    {
        foreach (IMyAirtightHangarDoor a in hangar)
        {
            a.OpenDoor();
        }
        Output("Opening in progress.");
    }
    else
    {
        foreach (IMyAirtightHangarDoor a in hangar)
        {
            a.CloseDoor();
        }
        Output("Closing in progress.");
    }
}

public void ToggleDoor(string argument)
{
    List<IMyAirtightHangarDoor> controledDoors;

    switch (argument)
    {
        case "LH":
            controledDoors = getLeftHangar();
            Output("\n\nLeft Hangar doors are ");
            break;

        case "RH":
            controledDoors = getRightHangar();
            Output("\n\nLeft Hangar doors are ");
            break;

        default:
            AlertOutput("\n\nWRONG ARGUMENT");
            return;
    }

    Output(statusToString(HangarStatus(controledDoors)));
    if (HangarStatus(controledDoors) == DoorStatus.Open || HangarStatus(controledDoors) == DoorStatus.Opening) DoorControl(controledDoors, false);
    else DoorControl(controledDoors, true);

}

public void Main(string argument, UpdateType updateSource)
{
    DefaultLook();

    switch (argument)
    {
        case "status":
            WriteStatus();
            break;

        case "LHO":
            DoorControl("LH", true);
            break;

        case "RHO":
            DoorControl("RH", true);
            break;

        case "LHC":
            DoorControl("LH", false);
            break;

        case "RHC":
            DoorControl("RH", false);
            break;

        case "LHT":
            ToggleDoor("LH");
            break;

        case "RHT":
            ToggleDoor("RH");
            break;

        default:
            AlertOutput("UNKNOWN COMMAND");
            break;
    }
}