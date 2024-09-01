﻿using System.Collections.ObjectModel;
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

		DisableMw2UiElements();
		InitializeClients();
	}

	private async void ConnectClicked(object sender, EventArgs e)
	{
		ConnectBtn.IsEnabled = false;
		await xbox.ConnectAsync("192.168.178.38");

		mw2 = new MW2(xbox);

		// If game is mw2, do

		await RefreshClients();

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

	private void DisableMw2UiElements() {
		RefreshBtn.IsEnabled = false;
		MessageEntry.IsEnabled = false;
		KickBtn.IsEnabled = false;
		DerankBtn.IsEnabled = false;
		FreezeConsoleBtn.IsEnabled = false;
		FreezeClassesBtn.IsEnabled = false;
	}

	private void EnableMw2UiElements() {
		RefreshBtn.IsEnabled = true;
		MessageEntry.IsEnabled = true;
		KickBtn.IsEnabled = true;
		DerankBtn.IsEnabled = true;
		FreezeConsoleBtn.IsEnabled = true;
		FreezeClassesBtn.IsEnabled = true;
	}

	public ObservableCollection<Client> Clients { get; } = new();

	public Client? SelectedClient {get; set;}
}

public record Client(int Index, string Name);