﻿using AGVCore;
using Model.MDM;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using Tools;

namespace AGVComponents
{
    public class RoutePlan
    {
        public List<LandmarkInfo> RouteList = new List<LandmarkInfo>();
        public List<LandmarkInfo> CloseList = new List<LandmarkInfo>();
        public Stack PivotAnotherLand = new Stack();
        private IDictionary dic_Land = new Hashtable();
        private int OpenCount = 0;
        public static object GetRoutObj = new object();

        public int OpenAmount
        {
            get
            {
                if (OpenCount == 0)
                {
                    OpenCount = CoreData.AllLands.Count();
                }
                return OpenCount;
            }
        }

        public void RepairRoute()
        {
            try
            {
                bool isRepair = false;
                AcountDirect(ref RouteList);
                List<LandmarkInfo> OrialRoute = CreateDeepCopy<List<LandmarkInfo>>(RouteList);
                //找到计算的路线中的所有有分支的地标
                List<LandmarkInfo> InflatLands = OrialRoute.Where(p => p.sway != SwayEnum.None).ToList();
                for (int i = 0; i < InflatLands.Count; i++)
                {
                    for (int j = i + 1; j < InflatLands.Count; j++)
                    {
                        LandmarkInfo FirstLand = InflatLands[i];
                        LandmarkInfo SecondLand = InflatLands[j];
                        if (FirstLand != null && SecondLand != null)
                        {
                            //重现计算这两个分支点之间的路径
                            //然后和开始计算的路径中的这两个地标之间的地标个数进行比较
                            //如果新算出来的比之前两个地标之间的地标个数少的话那么就用
                            //新算出来的路径片段替换原来的线路

                            //提出这两个地标之间在原来线路中的路径地标
                            List<LandmarkInfo> FindOldRouteFeg = new List<LandmarkInfo>();
                            int FirstIndex = OrialRoute.FindIndex(p => p.LandmarkCode == FirstLand.LandmarkCode);
                            int SecondIndex = OrialRoute.FindIndex(p => p.LandmarkCode == SecondLand.LandmarkCode);
                            if (FirstIndex >= 0 && SecondIndex >= 0)
                            {
                                for (int k = FirstIndex; k <= SecondIndex; k++)
                                { FindOldRouteFeg.Add(CreateDeepCopy<LandmarkInfo>(OrialRoute[k])); }
                            }

                            //重新计算两交叉地标之间的路径
                            CloseList.Clear();
                            RouteList.Clear();
                            AcountRoute(FirstLand, SecondLand);
                            //如果 新计算出来的两地标之间路径地标个数小于原先的那么则拿新算的路径地标信息替换原来的路径里面去
                            if (RouteList.Count > 0 && FindOldRouteFeg.Count > 0 && RouteList.Count < FindOldRouteFeg.Count)
                            {
                                OrialRoute.RemoveRange(FirstIndex, SecondIndex - FirstIndex + 1);
                                //再把新算的路径塞进去
                                OrialRoute.InsertRange(FirstIndex, RouteList);
                                isRepair = true;
                                break;
                            }
                        }
                    }
                    if (isRepair) { break; }
                }
                if (isRepair)
                {
                    RouteList = CreateDeepCopy<List<LandmarkInfo>>(OrialRoute);
                    RepairRoute();
                }
                else
                { RouteList = CreateDeepCopy<List<LandmarkInfo>>(OrialRoute); }
            }
            catch (Exception ex)
            { LogHelper.WriteErrorLog(ex); }
        }

        public List<LandmarkInfo> GetRoute(LandmarkInfo StartLand, LandmarkInfo EndLand)
        {
            try
            {
                lock (GetRoutObj)
                {
                    #region Old
                    if (StartLand == null || EndLand == null) { return new List<LandmarkInfo>(); }
                    RouteList.Clear();
                    CloseList.Clear();
                    PivotAnotherLand.Clear();
                    List<List<LandmarkInfo>> hs_route = new List<List<LandmarkInfo>>();
                    AcountRoute(StartLand, EndLand);
                    hs_route.Add(CreateDeepCopy<List<LandmarkInfo>>(RouteList));
                    List<LandmarkInfo> OrialRoute = CreateDeepCopy<List<LandmarkInfo>>(RouteList);
                    List<LandmarkInfo> OrialClose = CreateDeepCopy<List<LandmarkInfo>>(CloseList);
                    Stack NewPivotAnotherLand = CreateDeepCopy<Stack>(PivotAnotherLand);
                    while (NewPivotAnotherLand.Count > 0)
                    {
                        PivotAnotherLand.Clear();
                        RouteList = CreateDeepCopy<List<LandmarkInfo>>(OrialRoute);
                        CloseList = CreateDeepCopy<List<LandmarkInfo>>(OrialClose);
                        Hashtable hs = NewPivotAnotherLand.Pop() as Hashtable;
                        ArrayList keyList = new ArrayList(hs.Keys);
                        LandmarkInfo PivotLand = hs[keyList[0].ToString()] as LandmarkInfo;
                        try
                        {
                            int index = RouteList.FindIndex(p => p.LandmarkCode == keyList[0].ToString()) + 1;
                            int indexClose = CloseList.FindIndex(p => p.LandmarkCode == keyList[0].ToString()) + 1;
                            if (index >= 0 && index < RouteList.Count)
                            { RouteList.RemoveRange(index, RouteList.Count - index); }
                            if (indexClose >= 0 && indexClose < CloseList.Count)
                            { CloseList.RemoveRange(indexClose, CloseList.Count - indexClose); }
                        }
                        catch (Exception ex)
                        { LogHelper.WriteErrorLog(ex); }
                        AcountRoute(PivotLand, EndLand);
                        hs_route.Add(CreateDeepCopy<List<LandmarkInfo>>(RouteList));
                    }
                    List<LandmarkInfo> Routes = (from a in hs_route
                                                 where a.Where(p => p.LandmarkCode == EndLand.LandmarkCode).Count() > 0
                                                 orderby a.Count ascending
                                                 select a).FirstOrDefault();

                    if (Routes == null)
                    {
                        Routes = new List<LandmarkInfo>();
                        return Routes;
                    }
                    //AcountDirect(ref Routes);
                    //return Routes;
                    RouteList = CreateDeepCopy<List<LandmarkInfo>>(Routes);
                    #endregion
                    #region New
                    //CloseList.Clear();
                    //RouteList.Clear();
                    //AcountRoute(StartLand, EndLand);
                    RepairRoute();
                    AcountDirect(ref RouteList);
                    return RouteList;
                    #endregion
                }
            }
            catch (Exception ex)
            { LogHelper.WriteErrorLog(ex); return new List<LandmarkInfo>(); }
        }


        public void AcountRoute(LandmarkInfo StarLand, LandmarkInfo EndLand)
        {
            try
            {
                if (CoreData.AllSeg.Count > 0)
                {
                    LandmarkInfo PreLand = StarLand;
                    if (CloseList.Count < OpenAmount && PreLand.LandmarkCode != EndLand.LandmarkCode)
                    {
                        if (CloseList.Where(p => p.LandmarkCode == StarLand.LandmarkCode).Count() <= 0)
                        {
                            RouteList.Add(CreateDeepCopy<LandmarkInfo>(StarLand));
                            CloseList.Add(CreateDeepCopy<LandmarkInfo>(StarLand));
                        }
                        List<LandmarkInfo> lands = getNextLand(PreLand);
                        if (lands.Count == 0)
                        {
                            if (PivotAnotherLand.Count > 0)
                            {
                                Hashtable hs = PivotAnotherLand.Pop() as Hashtable;
                                ArrayList keyList = new ArrayList(hs.Keys);
                                LandmarkInfo PivotLand = hs[keyList[0].ToString()] as LandmarkInfo;
                                int index = RouteList.FindIndex(p => p.LandmarkCode == keyList[0].ToString()) + 1;
                                RouteList.RemoveRange(index, RouteList.Count - index);
                                PreLand = PivotLand;
                            }
                            else
                            { return; }
                        }
                        else if (lands.Count == 1)
                        {
                            if (CloseList.Where(p => p.LandmarkCode == lands[0].LandmarkCode).Count() <= 0)
                            {
                                RouteList.Add(CreateDeepCopy<LandmarkInfo>(lands[0]));
                                CloseList.Add(CreateDeepCopy<LandmarkInfo>(lands[0]));
                            }
                            PreLand = lands[0];
                        }
                        else
                        {
                            List<LandmarkInfo> CloseLand = (from a in lands
                                                            orderby getDistant(a.LandX, a.LandY, EndLand.LandX, EndLand.LandY) ascending
                                                            select a).ToList();

                            if (CloseLand == null)
                            {
                                throw (new Exception("计算路径异常!"));
                            }
                            else
                            {
                                if (CloseList.Where(p => p.LandmarkCode == CloseLand[0].LandmarkCode).Count() <= 0)
                                {
                                    RouteList.Add(CreateDeepCopy<LandmarkInfo>(CloseLand[0]));
                                    CloseList.Add(CreateDeepCopy<LandmarkInfo>(CloseLand[0]));
                                    for (int i = 1; i < CloseLand.Count; i++)
                                    {
                                        Hashtable hs = new Hashtable();
                                        hs[PreLand.LandmarkCode] = CloseLand[i];
                                        PivotAnotherLand.Push(hs);
                                    }
                                    //Hashtable hs = new Hashtable();
                                    //hs[PreLand.LandmarkCode] = CloseLand[1];
                                    //PivotAnotherLand.Push(hs);
                                }
                                PreLand = CloseLand[0];
                            }
                        }
                        AcountRoute(PreLand, EndLand);
                    }
                }//end
            }
            catch (Exception ex)
            { throw ex; }
        }

        /// <summary>
        /// 得到路线的地标集合
        /// </summary>
        public List<string> GetRouteStr(List<LandmarkInfo> Route)
        {
            try
            {
                List<LandmarkInfo> CopyRoutes = CreateDeepCopy<List<LandmarkInfo>>(Route);
                List<LandmarkInfo> ResultRoute = CreateDeepCopy<List<LandmarkInfo>>(Route);
                foreach (LandmarkInfo item in CopyRoutes)
                {
                    if (item.sway != SwayEnum.None)
                    {
                        List<LandmarkInfo> CloseLand = (from a in CoreData.AllLands
                                                        where a.LandmarkCode != item.LandmarkCode
                                                        orderby getDistant(a.LandX, a.LandY, item.LandX, item.LandY) ascending
                                                        select a).ToList();
                        int IndexItem = ResultRoute.FindIndex(p => p.LandmarkCode == item.LandmarkCode);
                        int Count = 1;
                        int InsetCount = 1;
                        foreach (LandmarkInfo Copy in CloseLand)
                        {
                            if (Count == 5) { break; }
                            if (ResultRoute.Where(p => p.LandmarkCode == Copy.LandmarkCode).Count() <= 0)
                            {
                                LandmarkInfo newLand = CreateDeepCopy<LandmarkInfo>(Copy);
                                ResultRoute.Insert(IndexItem + InsetCount, newLand);
                                InsetCount++;
                            }
                            Count++;
                        }
                    }
                }
                string RouteStr = string.Join(",", ResultRoute.Distinct().Select(p => p.LandmarkCode));
                List<string> ResultStr = RouteStr.Split(',').ToList<string>();
                return ResultStr;
            }
            catch (Exception ex)
            { throw ex; }
        }

        /// <summary>
        /// 得到下一个地标
        /// </summary>
        /// <returns></returns>
        private List<LandmarkInfo> getNextLand(LandmarkInfo PreLand)
        {
            try
            {
                List<LandmarkInfo> lands = new List<LandmarkInfo>();
                List<AllSegment> Children = CoreData.AllSeg.Where(p => p.BeginLandMakCode == PreLand.LandmarkCode).ToList();
                foreach (AllSegment item in Children)
                {
                    if (CloseList.Where(p => p.LandmarkCode == item.EndLandMarkCode).Count() <= 0)
                    {
                        LandmarkInfo EndLandMark = CoreData.AllLands.FirstOrDefault(p => p.LandmarkCode == item.EndLandMarkCode);
                        lands.Add(EndLandMark);
                    }
                }
                return lands;
            }
            catch (Exception ex)
            { throw ex; }
        }

        /// <summary>
        /// 得到路线分支中的令一个地标
        /// </summary>
        /// <param name="Route"></param>
        /// <param name="PreLand"></param>
        /// <returns></returns>
        private List<LandmarkInfo> getRouteEmbranchmentAnothertLand(List<LandmarkInfo> Route, LandmarkInfo PreLand)
        {
            try
            {
                List<LandmarkInfo> lands = new List<LandmarkInfo>();
                List<AllSegment> Children = CoreData.AllSeg.Where(p => p.BeginLandMakCode == PreLand.LandmarkCode).ToList();
                foreach (AllSegment item in Children)
                {
                    if (Route.Where(p => p.LandmarkCode == item.EndLandMarkCode).Count() <= 0)
                    {
                        LandmarkInfo EndLandMark = CoreData.AllLands.FirstOrDefault(p => p.LandmarkCode == item.EndLandMarkCode);
                        lands.Add(EndLandMark);
                    }
                }
                return lands;
            }
            catch (Exception ex)
            { LogHelper.WriteErrorLog(ex); return new List<LandmarkInfo>(); }
        }

        /// <summary>
        /// 计算两点之间距离
        /// </summary>
        /// <returns></returns>
        private double getDistant(double X1, double Y1, double X2, double Y2)
        {
            try
            {
                double distant = Math.Sqrt(Math.Pow(Math.Abs(X1 - X2), 2) + Math.Pow(Math.Abs(Y1 - Y2), 2));
                return distant;
            }
            catch (Exception ex)
            { throw ex; }
        }

        //copy对象
        public T CreateDeepCopy<T>(T obj)
        {
            T t;
            MemoryStream memoryStream = new MemoryStream();
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Serialize(memoryStream, obj);
            memoryStream.Position = 0;
            t = (T)formatter.Deserialize(memoryStream);
            return t;
        }

        //计算转向和前进和后退
        public void AcountDirect(ref List<LandmarkInfo> Routes)
        {
            try
            {
                if (Routes.Count <= 1) { return; }
                List<LandmarkInfo> CopyRoute = Routes;
                LandmarkInfo UpLand = null;
                LandmarkInfo InflectLand = null;
                LandmarkInfo NextLand = null;
                int IsCountAngel = 0;
                string IsCountAngelStr = CoreData.SysParameter["CountTurnType"].ToString();
                if (IsCountAngelStr == "角度")
                { IsCountAngel = 1; }

                string IsCountStraightLineTurn = CoreData.SysParameter["IsCountStraightLineTurn"].ToString();

                if (Routes.Count >= 3)
                {
                    for (int i = 2; i < Routes.Count; i++)
                    {
                        UpLand = Routes[i - 2];
                        InflectLand = Routes[i - 1];
                        NextLand = Routes[i];

                        if (IsCountAngel != 0)
                        {
                            double SP = (Math.Round(UpLand.LandX, 3) - Math.Round(NextLand.LandX, 3)) * (Math.Round(InflectLand.LandY, 3) - Math.Round(NextLand.LandY, 3)) - (Math.Round(UpLand.LandY, 3) - Math.Round(NextLand.LandY, 3)) * (Math.Round(InflectLand.LandX, 3) - Math.Round(NextLand.LandX, 3));
                            if (SP > 0.05)
                            {
                                InflectLand.sway = SwayEnum.Left;
                            }
                            else if (SP < -0.05)
                            {
                                InflectLand.sway = SwayEnum.Right;
                            }
                            else
                            { InflectLand.sway = SwayEnum.None; }
                        }

                        if (i != Routes.Count - 1)
                        {
                            #region 计算转向
                            if (IsCountAngel == 0)
                            {
                                if (i == 2)
                                {
                                    AllSegment Segment = CoreData.AllSeg.FirstOrDefault(p => p.BeginLandMakCode == UpLand.LandmarkCode && p.EndLandMarkCode == InflectLand.LandmarkCode);
                                    if (Segment != null && Segment.ExcuteTurnDirect != -1)
                                    {
                                        UpLand.sway = (SwayEnum)Segment.ExcuteTurnDirect;
                                    }
                                    else
                                    {
                                        if (CoreData.AllSeg.Count(p => p.BeginLandMakCode == UpLand.LandmarkCode/* && CopyRoute.Count(q => q.LandmarkCode == p.EndLandMarkCode) <= 0*/) > 0)
                                        {
                                            double SP = (Math.Round(UpLand.LandX, 3) - Math.Round(NextLand.LandX, 3)) * (Math.Round(InflectLand.LandY, 3) - Math.Round(NextLand.LandY, 3)) - (Math.Round(UpLand.LandY, 3) - Math.Round(NextLand.LandY, 3)) * (Math.Round(InflectLand.LandX, 3) - Math.Round(NextLand.LandX, 3));
                                            if (SP > 0.05)
                                            {
                                                UpLand.sway = SwayEnum.Left;
                                            }
                                            else if (SP < -0.05)
                                            {
                                                UpLand.sway = SwayEnum.Right;
                                            }
                                            else
                                            {
                                                if (!string.IsNullOrEmpty(IsCountStraightLineTurn) && IsCountStraightLineTurn.Trim() == "是")
                                                {
                                                    //磁导航需要判断分支的直线行径方向是否左右
                                                    List<LandmarkInfo> NextLands = getRouteEmbranchmentAnothertLand(Routes, UpLand);
                                                    LandmarkInfo AnothLand = NextLands.FirstOrDefault(p => p.LandmarkCode != InflectLand.LandmarkCode);
                                                    if (AnothLand != null)
                                                    {
                                                        SP = (Math.Round(UpLand.LandX, 3) - Math.Round(NextLand.LandX, 3)) * (Math.Round(AnothLand.LandY, 3) - Math.Round(NextLand.LandY, 3)) - (Math.Round(UpLand.LandY, 3) - Math.Round(NextLand.LandY, 3)) * (Math.Round(AnothLand.LandX, 3) - Math.Round(NextLand.LandX, 3));
                                                        if (SP > 0.05)
                                                        {
                                                            UpLand.sway = SwayEnum.Left;
                                                        }
                                                        else if (SP < -0.05)
                                                        {
                                                            UpLand.sway = SwayEnum.Right;
                                                        }
                                                        else
                                                        {
                                                            UpLand.sway = SwayEnum.None;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    if (Segment != null && Segment.ExcuteMoveDirect != -1)
                                    {
                                        UpLand.movedirect = (MoveDirectEnum)Segment.ExcuteMoveDirect;
                                    }

                                }





                                //else
                                //    {

                                AllSegment SegmentFlat = CoreData.AllSeg.FirstOrDefault(p => p.BeginLandMakCode == InflectLand.LandmarkCode && p.EndLandMarkCode == NextLand.LandmarkCode);
                                if (SegmentFlat != null && SegmentFlat.ExcuteTurnDirect != -1)
                                {
                                    InflectLand.sway = (SwayEnum)SegmentFlat.ExcuteTurnDirect;
                                }
                                else
                                {
                                    if (CoreData.AllSeg.Count(p => p.BeginLandMakCode == InflectLand.LandmarkCode/* && CopyRoute.Count(q => q.LandmarkCode == p.EndLandMarkCode) <= 0*/) > 0)
                                    {
                                        var A = Math.Atan2(Math.Round(NextLand.LandY, 3) - Math.Round(InflectLand.LandY, 3), Math.Round(NextLand.LandX, 3) - Math.Round(InflectLand.LandX, 3));
                                        var B = Math.Atan2(Math.Round(UpLand.LandY, 3) - Math.Round(InflectLand.LandY, 3), Math.Round(UpLand.LandX, 3) - Math.Round(InflectLand.LandX, 3));
                                        var C = B - A;
                                        double angel = (180 * (float)C) / Math.PI;
                                        double SP = (Math.Round(UpLand.LandX, 3) - Math.Round(NextLand.LandX, 3)) * (Math.Round(InflectLand.LandY, 3) - Math.Round(NextLand.LandY, 3)) - (Math.Round(UpLand.LandY, 3) - Math.Round(NextLand.LandY, 3)) * (Math.Round(InflectLand.LandX, 3) - Math.Round(NextLand.LandX, 3));
                                        if (SP > 0.05)
                                        {
                                            if (Math.Abs(angel) <= 90)
                                            {
                                                if (angel < 0)
                                                { InflectLand.sway = SwayEnum.Left; }
                                                else
                                                { InflectLand.sway = SwayEnum.Right; }
                                            }
                                            else
                                            { InflectLand.sway = SwayEnum.Left; }
                                        }
                                        else if (SP < -0.05)
                                        {
                                            if (Math.Abs(angel) <= 90)
                                            {
                                                if (angel < 0)
                                                { InflectLand.sway = SwayEnum.Left; }
                                                else
                                                { InflectLand.sway = SwayEnum.Right; }
                                            }
                                            else
                                            { InflectLand.sway = SwayEnum.Right; }
                                        }
                                        else
                                        {
                                            if (!string.IsNullOrEmpty(IsCountStraightLineTurn) && IsCountStraightLineTurn.Trim() == "是")
                                            {
                                                //磁导航需要判断分支的直线行径方向是否左右
                                                List<LandmarkInfo> NextLands = getRouteEmbranchmentAnothertLand(Routes, InflectLand);
                                                LandmarkInfo AnothLand = NextLands.FirstOrDefault(p => p.LandmarkCode != InflectLand.LandmarkCode);
                                                if (AnothLand != null)
                                                {
                                                    //SP = (Math.Round(UpLand.LandX, 3) - Math.Round(NextLand.LandX, 3)) * (Math.Round(AnothLand.LandY, 3) - Math.Round(NextLand.LandY, 3)) - (Math.Round(UpLand.LandY, 3) - Math.Round(NextLand.LandY, 3)) * (Math.Round(AnothLand.LandX, 3) - Math.Round(NextLand.LandX, 3));
                                                    SP = (Math.Round(UpLand.LandX, 3) - Math.Round(AnothLand.LandX, 3)) * (Math.Round(InflectLand.LandY, 3) - Math.Round(AnothLand.LandY, 3)) - (Math.Round(UpLand.LandY, 3) - Math.Round(AnothLand.LandY, 3)) * (Math.Round(InflectLand.LandX, 3) - Math.Round(AnothLand.LandX, 3));
                                                    if (SP > 0.05)
                                                    { InflectLand.sway = SwayEnum.Right; }
                                                    else if (SP < -0.05)
                                                    { InflectLand.sway = SwayEnum.Left; }
                                                    else
                                                    { InflectLand.sway = SwayEnum.None; }
                                                }
                                            }
                                        }
                                    }
                                }
                                if (SegmentFlat != null && SegmentFlat.ExcuteMoveDirect != -1)
                                {
                                    InflectLand.movedirect = (MoveDirectEnum)SegmentFlat.ExcuteMoveDirect;
                                }
                                else
                                {
                                    //判断退进
                                    var A = Math.Atan2(Math.Round(NextLand.LandY, 3) - Math.Round(InflectLand.LandY, 3), Math.Round(NextLand.LandX, 3) - Math.Round(InflectLand.LandX, 3));
                                    var B = Math.Atan2(Math.Round(UpLand.LandY, 3) - Math.Round(InflectLand.LandY, 3), Math.Round(UpLand.LandX, 3) - Math.Round(InflectLand.LandX, 3));
                                    var C = B - A;
                                    double angel = (180 * (float)C) / Math.PI;
                                    if (Math.Abs(angel) < 90 || (angel < 0 && Math.Round(angel, 0) < -270))
                                    { InflectLand.movedirect = MoveDirectEnum.Reverse; }
                                }
                                //}
                            }
                            #endregion

                            #region 计算角度
                            else
                            {
                                if (Math.Abs(Math.Round(UpLand.LandX, 2, MidpointRounding.AwayFromZero) - Math.Round(InflectLand.LandX, 2, MidpointRounding.AwayFromZero))<=0.1)
                                {
                                    //找对应的行走线段
                                    AllSegment Seg = CoreData.AllSeg.FirstOrDefault(p => p.BeginLandMakCode == UpLand.LandmarkCode && p.EndLandMarkCode == InflectLand.LandmarkCode);
                                    if (Seg != null && Seg.ExcuteAngle != -1)
                                    { UpLand.Angle = Seg.ExcuteAngle; }
                                    else
                                    {
                                        if (InflectLand.LandY > UpLand.LandY)//地图上北
                                        {
                                            if (CoreData.AGVCoordinate.Rows.Count > 0)
                                            {
                                                DataRow dr = CoreData.AGVCoordinate.Select("Direction=" + 0).FirstOrDefault();
                                                UpLand.Angle = Convert.ToInt16(dr["Angle"]);
                                            }
                                            else
                                            { UpLand.Angle = 90; }
                                        }
                                        else//地图下南
                                        {
                                            if (CoreData.AGVCoordinate.Rows.Count > 0)
                                            {
                                                DataRow dr = CoreData.AGVCoordinate.Select("Direction=" + 2).FirstOrDefault();
                                                UpLand.Angle = Convert.ToInt16(dr["Angle"]);
                                            }
                                            else
                                            { UpLand.Angle = 270; }
                                        }
                                    }
                                }
                                else if (Math.Abs(Math.Round(UpLand.LandY,2, MidpointRounding.AwayFromZero) - Math.Round(InflectLand.LandY,2, MidpointRounding.AwayFromZero))<=0.1)
                                {
                                    //找对应的行走线段
                                    AllSegment Seg = CoreData.AllSeg.FirstOrDefault(p => p.BeginLandMakCode == UpLand.LandmarkCode && p.EndLandMarkCode == InflectLand.LandmarkCode);
                                    if (Seg != null && Seg.ExcuteAngle != -1)
                                    { UpLand.Angle = Seg.ExcuteAngle; }
                                    else
                                    {
                                        if (InflectLand.LandX > UpLand.LandX)//地图右东
                                        {
                                            if (CoreData.AGVCoordinate.Rows.Count > 0)
                                            {
                                                DataRow dr = CoreData.AGVCoordinate.Select("Direction=" + 1).FirstOrDefault();
                                                UpLand.Angle = Convert.ToInt16(dr["Angle"]);
                                            }
                                            else
                                            { UpLand.Angle = 0; }
                                        }
                                        else//地图左西
                                        {
                                            if (CoreData.AGVCoordinate.Rows.Count > 0)
                                            {
                                                DataRow dr = CoreData.AGVCoordinate.Select("Direction=" + 3).FirstOrDefault();
                                                UpLand.Angle = Convert.ToInt16(dr["Angle"]);
                                            }
                                            else
                                            { UpLand.Angle = 180; }
                                        }
                                    }
                                }

                                ////////////////////////////////////////
                                if (Math.Abs(Math.Round(InflectLand.LandX, 2, MidpointRounding.AwayFromZero) - Math.Round(NextLand.LandX, 2, MidpointRounding.AwayFromZero))<=0.1)
                                {
                                    //找对应的行走线段
                                    AllSegment Seg = CoreData.AllSeg.FirstOrDefault(p => p.BeginLandMakCode == InflectLand.LandmarkCode && p.EndLandMarkCode == NextLand.LandmarkCode);
                                    if (Seg != null && Seg.ExcuteAngle != -1)
                                    { InflectLand.Angle = Seg.ExcuteAngle; }
                                    else
                                    {
                                        if (NextLand.LandY > InflectLand.LandY)//地图上北
                                        {
                                            if (CoreData.AGVCoordinate.Rows.Count > 0)
                                            {
                                                DataRow dr = CoreData.AGVCoordinate.Select("Direction=" + 0).FirstOrDefault();
                                                InflectLand.Angle = Convert.ToInt16(dr["Angle"]);
                                            }
                                            else
                                            { InflectLand.Angle = 90; }
                                        }
                                        else//地图下南
                                        {
                                            if (CoreData.AGVCoordinate.Rows.Count > 0)
                                            {
                                                DataRow dr = CoreData.AGVCoordinate.Select("Direction=" + 2).FirstOrDefault();
                                                InflectLand.Angle = Convert.ToInt16(dr["Angle"]);
                                            }
                                            else
                                            { InflectLand.Angle = 270; }
                                        }
                                    }
                                }
                                else if (Math.Abs(Math.Round(InflectLand.LandY, 2, MidpointRounding.AwayFromZero) - Math.Round(NextLand.LandY, 2, MidpointRounding.AwayFromZero))<=0.1)
                                {
                                    AllSegment Seg = CoreData.AllSeg.FirstOrDefault(p => p.BeginLandMakCode == InflectLand.LandmarkCode && p.EndLandMarkCode == NextLand.LandmarkCode);
                                    if (Seg != null && Seg.ExcuteAngle != -1)
                                    { InflectLand.Angle = Seg.ExcuteAngle; }
                                    else
                                    {
                                        if (NextLand.LandX > InflectLand.LandX)//地图右东
                                        {
                                            if (CoreData.AGVCoordinate.Rows.Count > 0)
                                            {
                                                DataRow dr = CoreData.AGVCoordinate.Select("Direction=" + 1).FirstOrDefault();
                                                InflectLand.Angle = Convert.ToInt16(dr["Angle"]);
                                            }
                                            else
                                            { InflectLand.Angle = 0; }
                                        }
                                        else//地图左西
                                        {
                                            if (CoreData.AGVCoordinate.Rows.Count > 0)
                                            {
                                                DataRow dr = CoreData.AGVCoordinate.Select("Direction=" + 3).FirstOrDefault();
                                                InflectLand.Angle = Convert.ToInt16(dr["Angle"]);
                                            }
                                            else
                                            { InflectLand.Angle = 180; }
                                        }
                                    }
                                }
                            }
                            #endregion

                            #region 设置速度和避障
                            AllSegment Segmentt = CoreData.AllSeg.FirstOrDefault(p => p.BeginLandMakCode == UpLand.LandmarkCode && p.EndLandMarkCode == InflectLand.LandmarkCode);
                            if (Segmentt != null && Segmentt.ExcuteAvoidance != -1)
                            { UpLand.ExcuteAvoidance = Segmentt.ExcuteAvoidance; }
                            if (Segmentt != null && Segmentt.ExcuteSpeed != -1)
                            { UpLand.ExcuteSpeed = Segmentt.ExcuteSpeed; }
                            #endregion
                        }
                        else
                        {
                            #region 计算转向
                            if (IsCountAngel == 0)
                            {
                                //AllSegment SegmentUPLand = CoreData.AllSeg.FirstOrDefault(p => p.BeginLandMakCode == UpLand.LandmarkCode && p.EndLandMarkCode == InflectLand.LandmarkCode);
                                //if (SegmentUPLand != null && SegmentUPLand.ExcuteTurnDirect != -1)
                                //    {
                                //    UpLand.sway = (SwayEnum)SegmentUPLand.ExcuteTurnDirect;
                                //    }
                                //else
                                //    {
                                //    if (CoreData.AllSeg.Count(p => p.BeginLandMakCode == UpLand.LandmarkCode && CopyRoute.Count(q => q.LandmarkCode == p.EndLandMarkCode) <= 0) > 0)
                                //        {
                                //        double SP = (Math.Round(UpLand.LandX, 3) - Math.Round(NextLand.LandX, 3)) * (Math.Round(InflectLand.LandY, 3) - Math.Round(NextLand.LandY, 3)) - (Math.Round(UpLand.LandY, 3) - Math.Round(NextLand.LandY, 3)) * (Math.Round(InflectLand.LandX, 3) - Math.Round(NextLand.LandX, 3));
                                //        if (SP > 0.05)
                                //            {
                                //            UpLand.sway = SwayEnum.Left;
                                //            }
                                //        else if (SP < -0.05)
                                //            {
                                //            UpLand.sway = SwayEnum.Right;
                                //            }
                                //        else
                                //            {
                                //            //磁导航需要判断分支的直线行径方向是否左右
                                //            List<LandmarkInfo> NextLands = getRouteEmbranchmentAnothertLand(Routes, UpLand);
                                //            LandmarkInfo AnothLand = NextLands.FirstOrDefault(p => p.LandmarkCode != InflectLand.LandmarkCode);
                                //            if (AnothLand != null)
                                //                {
                                //                SP = (Math.Round(UpLand.LandX, 3) - Math.Round(NextLand.LandX, 3)) * (Math.Round(AnothLand.LandY, 3) - Math.Round(NextLand.LandY, 3)) - (Math.Round(UpLand.LandY, 3) - Math.Round(NextLand.LandY, 3)) * (Math.Round(AnothLand.LandX, 3) - Math.Round(NextLand.LandX, 3));
                                //                if (SP > 0.05)
                                //                    {
                                //                    UpLand.sway = SwayEnum.Left;
                                //                    }
                                //                else if (SP < -0.05)
                                //                    {
                                //                    UpLand.sway = SwayEnum.Right;
                                //                    }
                                //                else
                                //                    {
                                //                    UpLand.sway = SwayEnum.None;
                                //                    }
                                //                }
                                //            }
                                //        }
                                //    }
                                //if (SegmentUPLand != null && SegmentUPLand.ExcuteMoveDirect != -1)
                                //    {
                                //    UpLand.movedirect = (MoveDirectEnum)SegmentUPLand.ExcuteMoveDirect;
                                //    }

                                AllSegment Segment = CoreData.AllSeg.FirstOrDefault(p => p.BeginLandMakCode == InflectLand.LandmarkCode && p.EndLandMarkCode == NextLand.LandmarkCode);
                                if (Segment != null && Segment.ExcuteTurnDirect != -1)
                                {
                                    InflectLand.sway = (SwayEnum)Segment.ExcuteTurnDirect;
                                }
                                else
                                {
                                    if (CoreData.AllSeg.Count(p => p.BeginLandMakCode == InflectLand.LandmarkCode/* && CopyRoute.Count(q => q.LandmarkCode == p.EndLandMarkCode) <= 0*/) > 0)
                                    {
                                        var A = Math.Atan2(Math.Round(NextLand.LandY, 3) - Math.Round(InflectLand.LandY, 3), Math.Round(NextLand.LandX, 3) - Math.Round(InflectLand.LandX, 3));
                                        var B = Math.Atan2(Math.Round(UpLand.LandY, 3) - Math.Round(InflectLand.LandY, 3), Math.Round(UpLand.LandX, 3) - Math.Round(InflectLand.LandX, 3));
                                        var C = B - A;
                                        double angel = (180 * (float)C) / Math.PI;
                                        double SP = (Math.Round(UpLand.LandX, 3) - Math.Round(NextLand.LandX, 3)) * (Math.Round(InflectLand.LandY, 3) - Math.Round(NextLand.LandY, 3)) - (Math.Round(UpLand.LandY, 3) - Math.Round(NextLand.LandY, 3)) * (Math.Round(InflectLand.LandX, 3) - Math.Round(NextLand.LandX, 3));
                                        if (SP > 0.05)
                                        {
                                            if (Math.Abs(angel) <= 90)
                                            {
                                                if (angel < 0)
                                                { InflectLand.sway = SwayEnum.Left; }
                                                else
                                                { InflectLand.sway = SwayEnum.Right; }
                                            }
                                            else
                                            { InflectLand.sway = SwayEnum.Left; }
                                        }
                                        else if (SP < -0.05)
                                        {
                                            if (Math.Abs(angel) <= 90)
                                            {
                                                if (angel < 0)
                                                { InflectLand.sway = SwayEnum.Left; }
                                                else
                                                { InflectLand.sway = SwayEnum.Right; }
                                            }
                                            else
                                            { InflectLand.sway = SwayEnum.Right; }
                                        }
                                        else
                                        {
                                            if (!string.IsNullOrEmpty(IsCountStraightLineTurn) && IsCountStraightLineTurn.Trim() == "是")
                                            {
                                                //磁导航需要判断分支的直线行径方向是否左右
                                                List<LandmarkInfo> NextLands = getRouteEmbranchmentAnothertLand(Routes, InflectLand);
                                                LandmarkInfo AnothLand = NextLands.FirstOrDefault(p => p.LandmarkCode != InflectLand.LandmarkCode);
                                                if (AnothLand != null)
                                                {
                                                    //SP = (Math.Round(UpLand.LandX, 3) - Math.Round(NextLand.LandX, 3)) * (Math.Round(AnothLand.LandY, 3) - Math.Round(NextLand.LandY, 3)) - (Math.Round(UpLand.LandY, 3) - Math.Round(NextLand.LandY, 3)) * (Math.Round(AnothLand.LandX, 3) - Math.Round(NextLand.LandX, 3));
                                                    SP = (Math.Round(UpLand.LandX, 3) - Math.Round(AnothLand.LandX, 3)) * (Math.Round(InflectLand.LandY, 3) - Math.Round(AnothLand.LandY, 3)) - (Math.Round(UpLand.LandY, 3) - Math.Round(AnothLand.LandY, 3)) * (Math.Round(InflectLand.LandX, 3) - Math.Round(AnothLand.LandX, 3));
                                                    if (SP > 0.05)
                                                    { InflectLand.sway = SwayEnum.Right; }
                                                    else if (SP < -0.05)
                                                    { InflectLand.sway = SwayEnum.Left; }
                                                    else
                                                    { InflectLand.sway = SwayEnum.None; }
                                                }
                                            }
                                        }
                                    }
                                }
                                if (Segment != null && Segment.ExcuteMoveDirect != -1)
                                {
                                    InflectLand.movedirect = (MoveDirectEnum)Segment.ExcuteMoveDirect;
                                }
                                else
                                {
                                    //判断退进
                                    var A = Math.Atan2(Math.Round(NextLand.LandY, 3) - Math.Round(InflectLand.LandY, 3), Math.Round(NextLand.LandX, 3) - Math.Round(InflectLand.LandX, 3));
                                    var B = Math.Atan2(Math.Round(UpLand.LandY, 3) - Math.Round(InflectLand.LandY, 3), Math.Round(UpLand.LandX, 3) - Math.Round(InflectLand.LandX, 3));
                                    var C = B - A;
                                    double angel = (180 * (float)C) / Math.PI;
                                    if (Math.Abs(angel) < 90 || (angel < 0 && Math.Round(angel, 0) < -270))
                                    { InflectLand.movedirect = MoveDirectEnum.Reverse; }
                                }
                            }
                            #endregion

                            #region 计算角度
                            else
                            {
                                //double SP = (Math.Round(UpLand.LandX, 3) - Math.Round(NextLand.LandX, 3)) * (Math.Round(InflectLand.LandY, 3) - Math.Round(NextLand.LandY, 3)) - (Math.Round(UpLand.LandY, 3) - Math.Round(NextLand.LandY, 3)) * (Math.Round(InflectLand.LandX, 3) - Math.Round(NextLand.LandX, 3));
                                //if (SP > 0.05)
                                //{
                                //    InflectLand.sway = SwayEnum.Left;
                                //}
                                //else if (SP < -0.05)
                                //{
                                //    InflectLand.sway = SwayEnum.Right;
                                //}
                                //else
                                //{ InflectLand.sway = SwayEnum.None; }

                                if (Math.Abs(Math.Round(UpLand.LandX, 2, MidpointRounding.AwayFromZero) - Math.Round(InflectLand.LandX, 2, MidpointRounding.AwayFromZero))<=0.1)
                                {
                                    //找对应的行走线段
                                    AllSegment Seg = CoreData.AllSeg.FirstOrDefault(p => p.BeginLandMakCode == UpLand.LandmarkCode && p.EndLandMarkCode == InflectLand.LandmarkCode);
                                    if (Seg != null && Seg.ExcuteAngle != -1)
                                    { UpLand.Angle = Seg.ExcuteAngle; }
                                    else
                                    {
                                        if (InflectLand.LandY > UpLand.LandY)//地图上北
                                        {
                                            if (CoreData.AGVCoordinate.Rows.Count > 0)
                                            {
                                                DataRow dr = CoreData.AGVCoordinate.Select("Direction=" + 0).FirstOrDefault();
                                                UpLand.Angle = Convert.ToInt16(dr["Angle"]);
                                            }
                                            else
                                            { UpLand.Angle = 90; }
                                        }
                                        else//地图下南
                                        {
                                            if (CoreData.AGVCoordinate.Rows.Count > 0)
                                            {
                                                DataRow dr = CoreData.AGVCoordinate.Select("Direction=" + 2).FirstOrDefault();
                                                UpLand.Angle = Convert.ToInt16(dr["Angle"]);
                                            }
                                            else
                                            { UpLand.Angle = 270; }
                                        }
                                    }
                                }
                                else if (Math.Abs(Math.Round(UpLand.LandY, 2, MidpointRounding.AwayFromZero) - Math.Round(InflectLand.LandY, 2, MidpointRounding.AwayFromZero))<=0.1)
                                {
                                    //找对应的行走线段
                                    AllSegment Seg = CoreData.AllSeg.FirstOrDefault(p => p.BeginLandMakCode == UpLand.LandmarkCode && p.EndLandMarkCode == InflectLand.LandmarkCode);
                                    if (Seg != null && Seg.ExcuteAngle != -1)
                                    { UpLand.Angle = Seg.ExcuteAngle; }
                                    else
                                    {
                                        if (InflectLand.LandX > UpLand.LandX)//地图右东
                                        {
                                            if (CoreData.AGVCoordinate.Rows.Count > 0)
                                            {
                                                DataRow dr = CoreData.AGVCoordinate.Select("Direction=" + 1).FirstOrDefault();
                                                UpLand.Angle = Convert.ToInt16(dr["Angle"]);
                                            }
                                            else
                                            { UpLand.Angle = 0; }
                                        }
                                        else//地图左西
                                        {
                                            if (CoreData.AGVCoordinate.Rows.Count > 0)
                                            {
                                                DataRow dr = CoreData.AGVCoordinate.Select("Direction=" + 3).FirstOrDefault();
                                                UpLand.Angle = Convert.ToInt16(dr["Angle"]);
                                            }
                                            else
                                            { UpLand.Angle = 180; }
                                        }
                                    }
                                }


                                if (Math.Abs(Math.Round(InflectLand.LandX, 2, MidpointRounding.AwayFromZero) - Math.Round(NextLand.LandX, 2, MidpointRounding.AwayFromZero))<=0.1)
                                {
                                    //找对应的行走线段
                                    AllSegment Seg = CoreData.AllSeg.FirstOrDefault(p => p.BeginLandMakCode == InflectLand.LandmarkCode && p.EndLandMarkCode == NextLand.LandmarkCode);
                                    if (Seg != null && Seg.ExcuteAngle != -1)
                                    { InflectLand.Angle = Seg.ExcuteAngle; }
                                    else
                                    {
                                        if (NextLand.LandY > InflectLand.LandY)//地图上北
                                        {
                                            if (CoreData.AGVCoordinate.Rows.Count > 0)
                                            {
                                                DataRow dr = CoreData.AGVCoordinate.Select("Direction=" + 0).FirstOrDefault();
                                                InflectLand.Angle = Convert.ToInt16(dr["Angle"]);
                                            }
                                            else
                                            { InflectLand.Angle = 90; }
                                        }
                                        else//地图下南
                                        {
                                            if (CoreData.AGVCoordinate.Rows.Count > 0)
                                            {
                                                DataRow dr = CoreData.AGVCoordinate.Select("Direction=" + 2).FirstOrDefault();
                                                InflectLand.Angle = Convert.ToInt16(dr["Angle"]);
                                            }
                                            else
                                            { InflectLand.Angle = 270; }
                                        }
                                    }
                                }
                                else if (Math.Abs(Math.Round(InflectLand.LandY, 2, MidpointRounding.AwayFromZero) - Math.Round(NextLand.LandY, 2, MidpointRounding.AwayFromZero))<=0.1)
                                {
                                    AllSegment Seg = CoreData.AllSeg.FirstOrDefault(p => p.BeginLandMakCode == InflectLand.LandmarkCode && p.EndLandMarkCode == NextLand.LandmarkCode);
                                    if (Seg != null && Seg.ExcuteAngle != -1)
                                    { InflectLand.Angle = Seg.ExcuteAngle; }
                                    else
                                    {
                                        if (NextLand.LandX > InflectLand.LandX)//地图右东
                                        {
                                            if (CoreData.AGVCoordinate.Rows.Count > 0)
                                            {
                                                DataRow dr = CoreData.AGVCoordinate.Select("Direction=" + 1).FirstOrDefault();
                                                InflectLand.Angle = Convert.ToInt16(dr["Angle"]);
                                            }
                                            else
                                            { InflectLand.Angle = 0; }
                                        }
                                        else//地图左西
                                        {
                                            if (CoreData.AGVCoordinate.Rows.Count > 0)
                                            {
                                                DataRow dr = CoreData.AGVCoordinate.Select("Direction=" + 3).FirstOrDefault();
                                                InflectLand.Angle = Convert.ToInt16(dr["Angle"]);
                                            }
                                            else
                                            { InflectLand.Angle = 180; }
                                        }
                                    }
                                }

                                NextLand.Angle = InflectLand.Angle;
                            }
                            #endregion

                            #region 设置速度和避障
                            AllSegment Segmentt = CoreData.AllSeg.FirstOrDefault(p => p.BeginLandMakCode == UpLand.LandmarkCode && p.EndLandMarkCode == InflectLand.LandmarkCode);
                            if (Segmentt != null && Segmentt.ExcuteAvoidance != -1)
                            { UpLand.ExcuteAvoidance = Segmentt.ExcuteAvoidance; }
                            if (Segmentt != null && Segmentt.ExcuteSpeed != -1)
                            { UpLand.ExcuteSpeed = Segmentt.ExcuteSpeed; }
                            Segmentt = CoreData.AllSeg.FirstOrDefault(p => p.BeginLandMakCode == InflectLand.LandmarkCode && p.EndLandMarkCode == NextLand.LandmarkCode);
                            if (Segmentt != null && Segmentt.ExcuteAvoidance != -1)
                            { InflectLand.ExcuteAvoidance = Segmentt.ExcuteAvoidance; }
                            if (Segmentt != null && Segmentt.ExcuteSpeed != -1)
                            { InflectLand.ExcuteSpeed = Segmentt.ExcuteSpeed; }
                            #endregion
                        }
                        if (IsCountAngel != 0)
                        {
                            if (UpLand.Angle != InflectLand.Angle)
                            { InflectLand.sway = SwayEnum.ExcuteLand; }
                            //else
                            //{
                            //    InflectLand.sway = SwayEnum.None;
                            //}
                        }
                    }//end foreach
                }
                else
                {
                    #region 计算转向
                    if (IsCountAngel == 0)
                    {
                        InflectLand = Routes[0];
                        NextLand = Routes[1];
                        AllSegment Segment = CoreData.AllSeg.FirstOrDefault(p => p.BeginLandMakCode == InflectLand.LandmarkCode && p.EndLandMarkCode == NextLand.LandmarkCode);
                        if (Segment != null && Segment.ExcuteTurnDirect != -1)
                        {
                            InflectLand.sway = (SwayEnum)Segment.ExcuteTurnDirect;
                        }
                        if (Segment != null && Segment.ExcuteMoveDirect != -1)
                        {
                            InflectLand.movedirect = (MoveDirectEnum)Segment.ExcuteMoveDirect;
                        }
                    }
                    #endregion

                    #region 计算角度
                    else
                    {
                        InflectLand = Routes[0];
                        NextLand = Routes[1];
                        if (Math.Abs(Math.Round(InflectLand.LandX, 2, MidpointRounding.AwayFromZero) - Math.Round(NextLand.LandX, 2, MidpointRounding.AwayFromZero))<=0.1)
                        {
                            //找对应的行走线段
                            AllSegment Seg = CoreData.AllSeg.FirstOrDefault(p => p.BeginLandMakCode == InflectLand.LandmarkCode && p.EndLandMarkCode == NextLand.LandmarkCode);
                            if (Seg != null && Seg.ExcuteAngle != -1)
                            { InflectLand.Angle = Seg.ExcuteAngle; }
                            else
                            {
                                if (NextLand.LandY > InflectLand.LandY)//地图上北
                                {
                                    if (CoreData.AGVCoordinate.Rows.Count > 0)
                                    {
                                        DataRow dr = CoreData.AGVCoordinate.Select("Direction=" + 0).FirstOrDefault();
                                        InflectLand.Angle = Convert.ToInt16(dr["Angle"]);
                                    }
                                    else
                                    { InflectLand.Angle = 90; }
                                }
                                else//地图下南
                                {
                                    if (CoreData.AGVCoordinate.Rows.Count > 0)
                                    {
                                        DataRow dr = CoreData.AGVCoordinate.Select("Direction=" + 2).FirstOrDefault();
                                        InflectLand.Angle = Convert.ToInt16(dr["Angle"]);
                                    }
                                    else
                                    { InflectLand.Angle = 270; }
                                }
                            }
                        }
                        else if (Math.Abs(Math.Round(InflectLand.LandY, 2, MidpointRounding.AwayFromZero) - Math.Round(NextLand.LandY, 2, MidpointRounding.AwayFromZero))<=0.1)
                        {
                            AllSegment Seg = CoreData.AllSeg.FirstOrDefault(p => p.BeginLandMakCode == InflectLand.LandmarkCode && p.EndLandMarkCode == NextLand.LandmarkCode);
                            if (Seg != null && Seg.ExcuteAngle != -1)
                            { InflectLand.Angle = Seg.ExcuteAngle; }
                            else
                            {
                                if (NextLand.LandX > InflectLand.LandX)//地图右东
                                {
                                    if (CoreData.AGVCoordinate.Rows.Count > 0)
                                    {
                                        DataRow dr = CoreData.AGVCoordinate.Select("Direction=" + 1).FirstOrDefault();
                                        InflectLand.Angle = Convert.ToInt16(dr["Angle"]);
                                    }
                                    else
                                    { InflectLand.Angle = 0; }
                                }
                                else//地图左西
                                {
                                    if (CoreData.AGVCoordinate.Rows.Count > 0)
                                    {
                                        DataRow dr = CoreData.AGVCoordinate.Select("Direction=" + 3).FirstOrDefault();
                                        InflectLand.Angle = Convert.ToInt16(dr["Angle"]);
                                    }
                                    else
                                    { InflectLand.Angle = 180; }
                                }
                            }
                        }
                    }
                    #endregion

                    #region 设置速度和避障
                    AllSegment Segmentt = CoreData.AllSeg.FirstOrDefault(p => p.BeginLandMakCode == InflectLand.LandmarkCode && p.EndLandMarkCode == NextLand.LandmarkCode);
                    if (Segmentt != null && Segmentt.ExcuteAvoidance != -1)
                    { InflectLand.ExcuteAvoidance = Segmentt.ExcuteAvoidance; }
                    if (Segmentt != null && Segmentt.ExcuteSpeed != -1)
                    { InflectLand.ExcuteSpeed = Segmentt.ExcuteSpeed; }
                    #endregion
                }
            }
            catch (Exception ex)
            { LogHelper.WriteErrorLog(ex); }
        }



        public static double Angle(Point cen, Point first, Point second)
        {
            try
            {
                const double M_PI = 3.1415926535897;

                double ma_x = first.X - cen.X;
                double ma_y = first.Y - cen.Y;
                double mb_x = second.X - cen.X;
                double mb_y = second.Y - cen.Y;
                double v1 = (ma_x * mb_x) + (ma_y * mb_y);
                double ma_val = Math.Sqrt(ma_x * ma_x + ma_y * ma_y);
                double mb_val = Math.Sqrt(mb_x * mb_x + mb_y * mb_y);
                double cosM = v1 / (ma_val * mb_val);
                double angleAMB = Math.Acos(cosM) * 180 / M_PI;

                return angleAMB;
            }
            catch (Exception ex)
            { throw ex; }
        }
    }//end
}
