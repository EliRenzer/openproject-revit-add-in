﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Bcfier.Revit.Data;
using Bcfier.Revit.Entry;
using System.ComponentModel;
using System.Threading.Tasks;
using Bcfier.Data.Utils;
using System.IO;
using Bcfier.ViewModels.Bcf;

namespace Bcfier.Revit
{
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class RevitWindow : Window
  {
    private ExternalEvent ExtEvent;
    private ExtEvntOpenView Handler;
    private UIApplication uiapp;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="_uiapp"></param>
    /// <param name="exEvent"></param>
    /// <param name="handler"></param>
    public RevitWindow(UIApplication _uiapp, ExternalEvent exEvent, ExtEvntOpenView handler)
    {
      InitializeComponent();

      try
      {
        ExtEvent = exEvent;
        Handler = handler;
        uiapp = _uiapp;
      }
      catch (Exception ex1)
      {
        TaskDialog.Show("Error!", "exception: " + ex1);
      }
    }

    #region commands
    /// <summary>
    /// Raises the External Event to accomplish a transaction in a modeless window
    /// http://help.autodesk.com/view/RVT/2014/ENU/?guid=GUID-0A0D656E-5C44-49E8-A891-6C29F88E35C0
    /// http://matteocominetti.com/starting-a-transaction-from-an-external-application-running-outside-of-api-context-is-not-allowed/
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnOpenView(object sender, ExecutedRoutedEventArgs e)
    {
      try
      {
        if (Bcfier.SelectedBcf() == null)
          return;
        var view = e.Parameter as BcfViewpointViewModel;
        if (view == null)
          return;
        UIDocument uidoc = uiapp.ActiveUIDocument;

        if (uidoc.ActiveView.ViewType == ViewType.Schedule)
        {
          MessageBox.Show("BCFier can't take snapshots of schedules.",
              "Warning!", MessageBoxButton.OK, MessageBoxImage.Warning);
          return;
        }

        if (uidoc.ActiveView.ViewType == ViewType.ThreeD)
        {
          var view3D = (View3D)uidoc.ActiveView;
          if (view3D.IsPerspective)
          {
            MessageBox.Show("This operation is not allowed in a Perspective View.\nPlease close the current window(s) and retry.",
                "Warning!", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
          }

        }
        Handler.v = view;
        ExtEvent.Raise();
      }
      catch (System.Exception ex1)
      {
        TaskDialog.Show("Error opening a View!", "exception: " + ex1);
      }
    }
    /// <summary>
    /// Same as in the windows app, but here we generate a VisInfo that is attached to the view
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnAddView(object sender, ExecutedRoutedEventArgs e)
    {
      try
      {

        if (Bcfier.SelectedBcf() == null)
          return;
        var issue = e.Parameter as BcfIssueViewModel;
        if (issue == null)
        {
          MessageBox.Show("No Issue selected", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
          return;
        }

        var dialog = new AddViewRevit(issue, Path.Combine(Path.GetTempPath(), Bcfier.SelectedBcf().Id.ToString()), uiapp.ActiveUIDocument.Document);
        dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        dialog.ShowDialog();
        if (dialog.DialogResult.HasValue && dialog.DialogResult.Value)
        {
          //generate and set the VisInfo
          var generatedViewpoint = RevitView.GenerateViewpoint(uiapp.ActiveUIDocument);
          issue.Viewpoints.Add(generatedViewpoint);

          //get filename
          UIDocument uidoc = uiapp.ActiveUIDocument;

          var headerFile = issue.Markup.HeaderFiles.FirstOrDefault();
          if (headerFile == null)
          {
            headerFile = new BcfHeaderFileViewModel();
            issue.Markup.HeaderFiles.Add(headerFile);
          }

          if (uidoc.Document.Title != null)
            headerFile.FileName = uidoc.Document.Title;
          else
            headerFile.FileName = "Unknown";

          Bcfier.SelectedBcf().IsModified = true;
        }

      }
      catch (System.Exception ex1)
      {
        TaskDialog.Show("Error adding a View!", "exception: " + ex1);
      }
    }
    #endregion

    #region private methods

    /// <summary>
    /// passing event to the user control
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Window_Closing(object sender, CancelEventArgs e)
    {
      e.Cancel = Bcfier.onClosing(e);
    }
    #endregion

    //stats
    private void RevitWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
      Task.Run(() =>
      {
        StatHat.Post.EzCounter(@"hello@teocomi.com", "BCFierRevitStart", 1);
      });
    }

  }
}