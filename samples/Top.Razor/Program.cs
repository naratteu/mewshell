using Aprillz.MewUI;
using Top.Razor;
using Naratteu.MewUI.Razor;

Application
    .Create()
    .UseMacOS()
    .UseMewVGMetal()
    .BuildMainWindow<App>()
    .Run();
