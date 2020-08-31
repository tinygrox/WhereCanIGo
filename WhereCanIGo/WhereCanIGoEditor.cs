﻿using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WhereCanIGo
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class WhereCanIGoEditor : MonoBehaviour
    {
        private ApplicationLauncherButton _toolbarButton;
        private PopupDialog _uiDialog;
        private readonly Rect _geometry = new Rect(0.5f, 0.5f, 700, 500);
        private bool _returnTrip;
        private Utilities _utilities;
        private bool payloadOnly;

        private void Awake()
        {
            GameEvents.onGUIApplicationLauncherReady.Add(AddToolbarButton);
            GameEvents.onGUIApplicationLauncherUnreadifying.Add(RemoveToolbarButton);
            _utilities = new Utilities();
        }

        private void AddToolbarButton()
        {
            if (_toolbarButton == null)
            {
                _toolbarButton = ApplicationLauncher.Instance.AddModApplication(SpawnDialog, SpawnDialog,
                    null, null, null, null, ApplicationLauncher.AppScenes.VAB,
                    GameDatabase.Instance.GetTexture("WhereCanIGo/Icon", false));
            }
        }

        private void RemoveToolbarButton(GameScenes whatever)
        {
            if (_toolbarButton != null) ApplicationLauncher.Instance.RemoveModApplication(_toolbarButton);
            _toolbarButton = null;
        }

        private void SpawnDialog()
        {
            if (_uiDialog == null) _uiDialog = GenerateDialog();
        }

        private PopupDialog GenerateDialog()
        {
            List<DialogGUIBase> guiItems = new List<DialogGUIBase>();
            if (EditorLogic.fetch == null || EditorLogic.fetch.ship == null)
            {
                guiItems.Add(new DialogGUILabel(LocalizedStrings.Dialog_title));//"No Vessel Detected"
            }
            else
            {
                guiItems.Add(new DialogGUILabel(_utilities.SystemNotes, _utilities.CreateNoteStyle()));
                guiItems.Add(new DialogGUILabel(_utilities.Warnings, _utilities.CreateNoteStyle()));
                DialogGUIBase[] vertical = new DialogGUIBase[_utilities.Planets.Count];
                DialogGUIBase[] horizontal = new DialogGUIBase[2];
                horizontal[0] = new DialogGUIToggle(() => _returnTrip, LocalizedStrings.IsReturn, delegate { SetReturnTrip(); }); //"Return Trip?"
                horizontal[1] = new DialogGUIToggle(() => payloadOnly, LocalizedStrings.PayloadOnly, delegate { SetPayoadOnly(); }); //"Payload Only"
                guiItems.Add(new DialogGUIHorizontalLayout(horizontal));
                for (int i = 0; i < _utilities.Planets.Count; i++)
                {
                    PlanetDeltaV p = _utilities.Planets.ElementAt(i);
                    horizontal = new DialogGUIBase[4];
                    horizontal[0] = new DialogGUILabel(p.GetName(), _utilities.GenerateStyle(-1, false));
                    horizontal[1] = GetDeltaVString(p, LocalizedStrings.Situation_Flyby + ": "); //Flyby
                    horizontal[2] = GetDeltaVString(p, LocalizedStrings.Situation_Orbiting + ": "); //Orbiting
                    if (p.IsHomeWorld && p.SynchronousDv != -1) horizontal[3] = GetDeltaVString(p, LocalizedStrings.Situation_SynchronousOrbit + ": "); //Synchronous Orbit
                    else horizontal[3] = GetDeltaVString(p, LocalizedStrings.Situation_Landing + ": "); //Landing
                    vertical[i] = new DialogGUIHorizontalLayout(horizontal);
                }
                DialogGUIVerticalLayout layout = new DialogGUIVerticalLayout(vertical);
                guiItems.Add(new DialogGUIScrollList(-Vector2.one, false, true, layout));
            }
            guiItems.Add(new DialogGUILabel(LocalizedStrings.AssumTips)); //"*Assuming craft has enough chutes"
            guiItems.Add(new DialogGUIButton(LocalizedStrings.CloseButton, () => _utilities.CloseDialog(_uiDialog), false)); //"Close"
            return PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new MultiOptionDialog("WhereCanIGoDialog", "", LocalizedStrings.Dialog_title2, UISkinManager.defaultSkin, // "Where Can I Go"
                    _geometry,
                    guiItems.ToArray()), false, UISkinManager.defaultSkin);
        }

        private void SetPayoadOnly()
        {
            payloadOnly = !payloadOnly;
            RefreshDialog();
        }


        private DialogGUILabel GetDeltaVString(PlanetDeltaV planet, string situation)
        {
            int SituationCases = 0;
            if (situation == LocalizedStrings.Situation_Flyby + ": ")
                SituationCases = 1;
            else if (situation == LocalizedStrings.Situation_Orbiting + ": ")
                SituationCases = 2;
            else if (situation == LocalizedStrings.Situation_Landing + ": ")
                SituationCases = 3;
            else if (situation == LocalizedStrings.Situation_SynchronousOrbit + ": ")
                SituationCases = 4;
            int deltaV = -1;
            string s;
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (SituationCases)
            {
                case 1://"Flyby: ":
                    deltaV = planet.EscapeDv;
                    if (_returnTrip) deltaV += planet.ReturnFromFlybyDv;
                    break;
                case 2://"Orbiting: ":
                    deltaV = planet.OrbitDv;
                    if (_returnTrip) deltaV += planet.ReturnFromOrbitDv;
                    break;
                case 3://"Landing: ":
                    deltaV = planet.LandDv;
                    if (_returnTrip) deltaV += planet.ReturnFromLandingDv;
                    break;
                case 4://"Synchronous Orbit: ":
                    deltaV = planet.SynchronousDv;
                    break;
            }

            if (payloadOnly) deltaV -= _utilities.ConvertBodyToPlanetDeltaV(FlightGlobals.GetHomeBody()).OrbitDv;
            UIStyle style = _utilities.GenerateStyle(deltaV, false);
            string status = _utilities.VesselStatus(deltaV, situation, planet);
            double shortFallOrDeficit = 0;
            if (EditorLogic.fetch.ship != null && EditorLogic.fetch.ship.vesselDeltaV != null)
            {
                shortFallOrDeficit =
                    Math.Round(Math.Abs(deltaV - EditorLogic.fetch.ship.vesselDeltaV.TotalDeltaVVac), 0);
            }
            if (status == "NO")
                status = status + " (" + shortFallOrDeficit +
                         "m/s short)";
            else status = status + " (+" + shortFallOrDeficit +
                          "m/s)";
            if (_utilities.SituationValid(planet.RelatedBody, situation)) s = " | " + situation + status;
            else s = " | " + situation + "N/A";
            return new DialogGUILabel(s, style);
        }

        private void OnDisable()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(AddToolbarButton);
            GameEvents.onGUIApplicationLauncherUnreadifying.Remove(RemoveToolbarButton);
        }

        private void SetReturnTrip()
        {
            _returnTrip = !_returnTrip;
            RefreshDialog();
        }

        private void RefreshDialog()
        {
            _utilities.CloseDialog(_uiDialog);
            Invoke(nameof(SpawnDialog), 0.1f);
        }
    }
}
