namespace FightSong.ReleaseChecks
{
    public static class ProjectPaneProbe
    {
        public static object Left()=>FightSong.InspectorTool.QuantumProjectPaneVerification.Click("Assets/FightSongStudio/QuantumInspector/Demo/Materials");
        public static object Right()=>FightSong.InspectorTool.QuantumProjectPaneVerification.Click("Assets/FightSongStudio/QuantumInspector/Demo/Materials/Amber.mat");
    }
}
