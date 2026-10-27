/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: MainPage.CatalogRow.cs
 * Purpose: Supplies bindable appearance and selection state for theme library rows.
 *
 * VibeWare: Human intent, AI execution, and plenty of tokens
 * Manifesto: https://umbertogiacobbi.biz/vibeware/manifesto
 *
 * Created with AI: OpenAI Codex; extracted existing first-party source; 2026-10-27.
 * Modified with AI: OpenAI Codex; source documentation and organization; 2026-10-27.
 * Human guidance: Umberto Giacobbi; maintainability and standard file headers.
 *
 * Copyright (c) 2026 Umberto Giacobbi
 * SPDX-License-Identifier: MIT
 *
 * VBWR E */


using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace ThermalrightVibeWare.App;

public sealed partial class MainPage
{
    /// <summary>Stores one bindable category/theme row and its selection appearance.</summary>
    /// <param name="id">Stable category or theme identifier from the engine catalog.</param>
    /// <param name="name">Visible catalog label.</param>
    /// <param name="description">Optional theme description shown for the selected row.</param>
    /// <param name="number">Optional formatted position in the theme list.</param>
    public sealed class CatalogRow(string id, string name, string description = "", string number = "") : INotifyPropertyChanged
    {
        public string Id { get; } = id;
        public string Name { get; } = name;
        public string Description { get; } = description;
        public Visibility DescriptionVisibility { get; private set; } = Visibility.Collapsed;
        public string Number { get; } = number;
        public Brush Outline { get; private set; } = new SolidColorBrush(Colors.Transparent);
        public FontWeight Weight { get; private set; } = FontWeights.Normal;
        public Brush Ink { get; private set; } = MainPage.Brush("#F1F1FA");
        public Brush Muted { get; private set; } = MainPage.Brush("#9C9CAF");
        public Brush Accent { get; private set; } = MainPage.Brush("#C9AEF3");
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Updates selection emphasis and notifies the bound library row controls.</summary>
        /// <param name="light">Whether to use the light appearance.</param>
        /// <param name="selected">Whether this row is the currently selected library entry.</param>
        public void Update(bool light, bool selected)
        {
            DescriptionVisibility = selected ? Visibility.Visible : Visibility.Collapsed;
            Outline = selected ? MainPage.Brush(light ? "#7854B5" : "#C9AEF3") : new SolidColorBrush(Colors.Transparent);
            Weight = selected ? FontWeights.SemiBold : FontWeights.Normal;
            Ink = MainPage.Brush(selected ? light ? "#68468F" : "#D8BAFA" : light ? "#242231" : "#F1F1FA");
            Muted = MainPage.Brush(light ? "#716B80" : "#9C9CAF");
            Accent = MainPage.Brush(selected ? light ? "#7854B5" : "#D8BAFA" : light ? "#A89BB7" : "#686B80");
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(""));
        }
    }
}
