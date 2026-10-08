using Aprillz.MewUI;
using GitView.Razor;
using Naratteu.MewUI.Razor;

Application
    .Create()
    .UseMacOS()
    .UseMewVGMetal()
    .BuildMainWindow<App>()
    .Run();
