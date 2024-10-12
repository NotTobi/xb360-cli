using System.Collections.ObjectModel;
using xbox;

namespace app;

public partial class MainPage : ContentPage
{
	private Xbox xbox = new Xbox();
	private MW2? mw2;

	public MainPage()
	{
		InitializeComponent();

		BindingContext = this;

		DisableGeneralUiElements();
		DisableMw2UiElements();
		InitializeClients();
	}

	private async void ConnectClicked(object sender, EventArgs e)
	{
		await xbox.ConnectAsync("192.168.178.30");

		EnableGeneralUiElements();

		mw2 = new MW2(xbox);

		EnableMw2UiElements();
	}

	private async void RefreshClicked(object sender, EventArgs e)
	{
		await RefreshClients();
	}

	private void InitializeClients() {
		Clients.Add(new Client(-1, "All"));

		for (int i = 0; i < 18; i++)
		{
			Clients.Add(new Client(i, ""));
		}
	}

	private async Task RefreshClients() 
	{
		Clients.Clear();

		Clients.Add(new Client(-1, "All"));

		for (int i = 0; i < 18; i++)
		{
			var name = await mw2!.GetName(i);
			Clients.Add(new Client(i, name));
		}
	}

	private async void SetPregameName(object sender, EventArgs e)
	{
		await mw2!.SetPregameName(NameEntry.Text);
	}

	private async void StartGameClicked(object sender, EventArgs e)
	{
		await mw2!.StartGameFromLobby();
	}

	private async void LaunchMW2Clicked(object sender, EventArgs e)
	{
		await xbox.LaunchXexAsync("/Games/MW2 TU9/default_mp.xex");
	}

	private async void SendMessage(object sender, EventArgs e)
	{
		var clientIndex = SelectedClient?.Index;
		var message = MessageEntry.Text;

		if(!clientIndex.HasValue || string.IsNullOrEmpty(message)){
			return;
		}

		await mw2!.SendMessage(clientIndex.Value, message);
	}

	private async void KickClicked(object sender, EventArgs e)
	{
		var clientIndex = SelectedClient?.Index;

		if(!clientIndex.HasValue){
			return;
		}

		await mw2!.Kick(clientIndex.Value);
	}

	private async void DerankClicked(object sender, EventArgs e)
	{
		var clientIndex = SelectedClient?.Index;

		if(!clientIndex.HasValue){
			return;
		}

		await mw2!.Derank(clientIndex.Value);
	}

	private async void FreezeConsoleClicked(object sender, EventArgs e)
	{
		var clientIndex = SelectedClient?.Index;

		if(!clientIndex.HasValue){
			return;
		}

		await mw2!.FreezeConsole(clientIndex.Value);
	}
	
	private async void FreezeClassesClicked(object sender, EventArgs e)
	{
		var clientIndex = SelectedClient?.Index;

		if(!clientIndex.HasValue){
			return;
		}

		await mw2!.FreezeClasses(clientIndex.Value);
	}

	private async void LowHealthClicked(object sender, EventArgs e)
	{
		await mw2!.SetLowHealth();
	}

	private async void BotsWarningClicked(object sender, EventArgs e)
	{
		await mw2!.SendMessage(-1, "^5Kill Bots^7 = ^1Kick");
		await Task.Delay(2000);
		var hostname = !string.IsNullOrEmpty(NameEntry.Text) ? NameEntry.Text : "me";
		await mw2!.SendMessage(-1, $"Msg ^2{hostname}^7 for ^5unlock all^7 or ^5last");
	}

	private void DisableGeneralUiElements() {
		LaunchMW2Btn.IsEnabled = false;
	}

	private void EnableGeneralUiElements() {
		LaunchMW2Btn.IsEnabled = true;
	}

	private void DisableMw2UiElements() {
		RefreshBtn.IsEnabled = false;
		MessageEntry.IsEnabled = false;
		KickBtn.IsEnabled = false;
		DerankBtn.IsEnabled = false;
		FreezeConsoleBtn.IsEnabled = false;
		FreezeClassesBtn.IsEnabled = false;
		StartGameBtn.IsEnabled = false;
		LowHealthBtn.IsEnabled = false;
		BotsWarningBtn.IsEnabled = false;
	}

	private void EnableMw2UiElements() {
		RefreshBtn.IsEnabled = true;
		MessageEntry.IsEnabled = true;
		KickBtn.IsEnabled = true;
		DerankBtn.IsEnabled = true;
		FreezeConsoleBtn.IsEnabled = true;
		FreezeClassesBtn.IsEnabled = true;
		StartGameBtn.IsEnabled = true;
		LowHealthBtn.IsEnabled = true;
		BotsWarningBtn.IsEnabled = true;
	}

	public ObservableCollection<Client> Clients { get; } = new();

	public Client? SelectedClient {get; set;}
}

public record Client(int Index, string Name);