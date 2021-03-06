﻿using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Bcfier.Data.Utils;
using Bcfier.Revit.Data;
using Bcfier.ViewModels.Bcf;

namespace Bcfier.Revit.Entry
{
  /// <summary>
  /// Obfuscation Ignore for External Interface
  /// </summary>
  public class ExtEvntOpenView : IExternalEventHandler
  {
    public BcfViewpointViewModel v;

    /// <summary>
    /// External Event Implementation
    /// </summary>
    /// <param name="app"></param>
    public void Execute(UIApplication app)
    {
      try
      {
        UIDocument uidoc = app.ActiveUIDocument;
        Document doc = uidoc.Document;
        var uniqueView = UserSettings.GetBool("alwaysNewView");

        // IS ORTHOGONAL
        if (v.OrthogonalCamera != null)
        {
          if (v.OrthogonalCamera == null)
            return;
          //type = "OrthogonalCamera";
          var zoom = v.OrthogonalCamera.ViewToWorldScale.ToFeet();
          var cameraDirection = RevitUtils.GetRevitXYZ(v.OrthogonalCamera.DirectionX,
            v.OrthogonalCamera.DirectionY,
            v.OrthogonalCamera.DirectionZ);
          var cameraUpVector = RevitUtils.GetRevitXYZ(v.OrthogonalCamera.UpX,
            v.OrthogonalCamera.UpY,
            v.OrthogonalCamera.UpZ);
          var cameraViewPoint = RevitUtils.GetRevitXYZ(v.OrthogonalCamera.ViewPointX,
            v.OrthogonalCamera.ViewPointY,
            v.OrthogonalCamera.ViewPointZ);
          var orient3D = RevitUtils.ConvertBasePoint(doc, cameraViewPoint, cameraDirection, cameraUpVector, true);

          View3D orthoView = null;
          //if active view is 3d ortho use it
          if (doc.ActiveView.ViewType == ViewType.ThreeD)
          {
            var activeView3D = doc.ActiveView as View3D;
            if (!activeView3D.IsPerspective)
              orthoView = activeView3D;
          }
          if (orthoView == null)
          {
            //try to use an existing 3D view
            IEnumerable<View3D> viewcollector3D = get3DViews(doc);
            if (viewcollector3D.Any(o => o.Name == "{3D}" || o.Name == "BCFortho"))
              orthoView = viewcollector3D.First(o => o.Name == "{3D}" || o.Name == "BCFortho");
          }
          using (var trans = new Transaction(uidoc.Document))
          {
            if (trans.Start("Open orthogonal view") == TransactionStatus.Started)
            {
              //create a new 3d ortho view 

              if (orthoView == null || uniqueView)
              {
                orthoView = View3D.CreateIsometric(doc, getFamilyViews(doc).First().Id);
                orthoView.Name = (uniqueView) ? "BCFortho" + DateTime.Now.ToString("yyyyMMddTHHmmss") : "BCFortho";
              }
              else
              {
                //reusing an existing view, I net to reset the visibility
                //placed this here because if set afterwards it doesn't work
                orthoView.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
              }
              orthoView.SetOrientation(orient3D);
              trans.Commit();
            }
          }
          uidoc.ActiveView = orthoView;
          //adjust view rectangle

          // **** CUSTOM VALUE FOR TEKLA **** //
          // double x = zoom / 2.5;
          // **** CUSTOM VALUE FOR TEKLA **** //

          double x = zoom;
          //if(MySettings.Get("optTekla")=="1")
          //    x = zoom / 2.5;

          //set UI view position and zoom
          XYZ m_xyzTl = uidoc.ActiveView.Origin.Add(uidoc.ActiveView.UpDirection.Multiply(x)).Subtract(uidoc.ActiveView.RightDirection.Multiply(x));
          XYZ m_xyzBr = uidoc.ActiveView.Origin.Subtract(uidoc.ActiveView.UpDirection.Multiply(x)).Add(uidoc.ActiveView.RightDirection.Multiply(x));
          uidoc.GetOpenUIViews().First().ZoomAndCenterRectangle(m_xyzTl, m_xyzBr);
        }
        //perspective
        else if (v.PerspectiveCamera != null)
        {
          if (v.PerspectiveCamera == null)
            return;

          //not used since the fov cannot be changed in Revit
          var zoom = v.PerspectiveCamera.FieldOfView;
          //FOV - not used
          //double z1 = 18 / Math.Tan(zoom / 2 * Math.PI / 180);
          //double z = 18 / Math.Tan(25 / 2 * Math.PI / 180);
          //double factor = z1 - z;

          var cameraDirection = RevitUtils.GetRevitXYZ(v.PerspectiveCamera.DirectionX,
            v.PerspectiveCamera.DirectionY,
            v.PerspectiveCamera.DirectionZ);
          var cameraUpVector = RevitUtils.GetRevitXYZ(v.PerspectiveCamera.UpX,
            v.PerspectiveCamera.UpY,
            v.PerspectiveCamera.UpZ);
          var cameraViewPoint = RevitUtils.GetRevitXYZ(v.PerspectiveCamera.ViewPointX,
            v.PerspectiveCamera.ViewPointY,
            v.PerspectiveCamera.ViewPointZ);
          var orient3D = RevitUtils.ConvertBasePoint(doc, cameraViewPoint, cameraDirection, cameraUpVector, true);



          View3D perspView = null;
          //try to use an existing 3D view
          IEnumerable<View3D> viewcollector3D = get3DViews(doc);
          if (viewcollector3D.Any(o => o.Name == "BCFpersp"))
            perspView = viewcollector3D.First(o => o.Name == "BCFpersp");

          using (var trans = new Transaction(uidoc.Document))
          {
            if (trans.Start("Open perspective view") == TransactionStatus.Started)
            {
              if (null == perspView || uniqueView)
              {
                perspView = View3D.CreatePerspective(doc, getFamilyViews(doc).First().Id);
                perspView.Name = (uniqueView) ? "BCFpersp" + DateTime.Now.ToString("yyyyMMddTHHmmss") : "BCFpersp";
              }
              else
              {
                //reusing an existing view, I net to reset the visibility
                //placed this here because if set afterwards it doesn't work
                perspView.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
              }

              perspView.SetOrientation(orient3D);

              // turn off the far clip plane
              if (perspView.get_Parameter(BuiltInParameter.VIEWER_BOUND_ACTIVE_FAR).HasValue)
              {
                Parameter m_farClip = perspView.get_Parameter(BuiltInParameter.VIEWER_BOUND_ACTIVE_FAR);
                m_farClip.Set(0);
              }
              perspView.CropBoxActive = true;
              perspView.CropBoxVisible = true;

              trans.Commit();
            }
          }
          uidoc.ActiveView = perspView;
        }
        //no view included
        else
          return;

        if (v.Components == null)
          return;


        var elementsToSelect = new List<ElementId>();
        var elementsToHide = new List<ElementId>();
        var elementsToShow = new List<ElementId>();

        var visibleElems = new FilteredElementCollector(doc, doc.ActiveView.Id)
        .WhereElementIsNotElementType()
        .WhereElementIsViewIndependent()
        .ToElementIds()
        .Where(e => doc.GetElement(e).CanBeHidden(doc.ActiveView)); //might affect performance, but it's necessary

        //loop elements
        foreach (var e in visibleElems)
        {
          var guid = IfcGuid.ToIfcGuid(ExportUtils.GetExportId(doc, e));

          if (v.Components.Any(c => !c.IsVisible && c.IfcGuid == guid))
          {
            elementsToHide.Add(e);
          }

          if (v.Components.Any(c => c.IsVisible && c.IfcGuid == guid))
          {
            elementsToShow.Add(e);
          }

          if (v.Components.Any(c => c.IsSelected && c.IfcGuid == guid))
          {
            elementsToShow.Add(e);
          }
        }

        using (var trans = new Transaction(uidoc.Document))
        {
          if (trans.Start("Apply BCF visibility and selection") == TransactionStatus.Started)
          {
            if (elementsToHide.Any())
              doc.ActiveView.HideElementsTemporary(elementsToHide);
            //there are no items to hide, therefore hide everything and just show the visible ones
            else if (elementsToShow.Any())
              doc.ActiveView.IsolateElementsTemporary(elementsToShow);

            if (elementsToSelect.Any())
              uidoc.Selection.SetElementIds(elementsToSelect);
          }
          trans.Commit();
        }


        uidoc.RefreshActiveView();
      }
      catch (Exception ex)
      {
        TaskDialog.Show("Error!", "exception: " + ex);
      }
    }
    private IEnumerable<ViewFamilyType> getFamilyViews(Document doc)
    {

      return from elem in new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType))
             let type = elem as ViewFamilyType
             where type.ViewFamily == ViewFamily.ThreeDimensional
             select type;
    }
    private IEnumerable<View3D> get3DViews(Document doc)
    {
      return from elem in new FilteredElementCollector(doc).OfClass(typeof(View3D))
             let view = elem as View3D
             select view;
    }
    private IEnumerable<View> getSheets(Document doc, int id, string stname)
    {
      ElementId eid = new ElementId(id);
      return from elem in new FilteredElementCollector(doc).OfClass(typeof(View))
             let view = elem as View
             //Get the view with the given Id or given name
             where view.Id == eid | view.Name == stname
             select view;
      
    }




    public string GetName()
    {
      return "Open 3D View";
    }
    // returns XYZ and ZOOM/FOV value
  }

}