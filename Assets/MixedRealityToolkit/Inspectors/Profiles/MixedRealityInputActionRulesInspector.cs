﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information. 

using Microsoft.MixedReality.Toolkit.Core.Definitions;
using Microsoft.MixedReality.Toolkit.Core.Definitions.InputSystem;
using Microsoft.MixedReality.Toolkit.Core.Definitions.Utilities;
using Microsoft.MixedReality.Toolkit.Core.Inspectors;
using Microsoft.MixedReality.Toolkit.Core.Inspectors.Profiles;
using Microsoft.MixedReality.Toolkit.Core.Inspectors.Utilities;
using Microsoft.MixedReality.Toolkit.Core.Services;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Inspectors.Profiles
{
    [CustomEditor(typeof(MixedRealityInputActionRulesProfile))]
    public class MixedRealityInputActionRulesInspector : BaseMixedRealityToolkitConfigurationProfileInspector
    {
        private static readonly GUIContent RuleAddButtonContent = new GUIContent("+ Add a New Rule Definition");
        private static readonly GUIContent RuleMinusButtonContent = new GUIContent("-", "Remove Rule Definition");
        private static readonly GUIContent BaseActionContent = new GUIContent("Base Input Action:", "The Action that will raise new actions based on the criteria met");
        private static readonly GUIContent RuleActionContent = new GUIContent("Rule Input Action:", "The Action that will be raised when the criteria is met");
        private static readonly GUIContent CriteriaContent = new GUIContent("Action Criteria:", "The Criteria that must be met in order to raise the new Action");

        private SerializedProperty inputActionRulesDigital;
        private SerializedProperty inputActionRulesSingleAxis;
        private SerializedProperty inputActionRulesDualAxis;
        private SerializedProperty inputActionRulesVectorAxis;
        private SerializedProperty inputActionRulesQuaternionAxis;
        private SerializedProperty inputActionRulesPoseAxis;

        private int[] baseActionIds;
        private string[] baseActionLabels;
        private static int[] ruleActionIds = new int[0];
        private static string[] ruleActionLabels = new string[0];

        private int selectedBaseActionId = 0;
        private int selectedRuleActionId = 0;

        private static MixedRealityInputAction currentBaseAction = MixedRealityInputAction.None;
        private static MixedRealityInputAction currentRuleAction = MixedRealityInputAction.None;

        private bool currentBoolCriteria;
        private float currentSingleAxisCriteria;
        private Vector2 currentDualAxisCriteria;
        private Vector3 currentVectorCriteria;
        private Quaternion currentQuaternionCriteria;
        private MixedRealityPose currentPoseCriteria;

        private bool[] digitalFoldouts;
        private bool[] singleAxisFoldouts;
        private bool[] dualAxisFoldouts;
        private bool[] vectorFoldouts;
        private bool[] quaternionFoldouts;
        private bool[] poseFoldouts;

        private MixedRealityInputActionRulesProfile thisProfile;

        protected override void OnEnable()
        {
            base.OnEnable();

            if (!MixedRealityInspectorUtility.CheckMixedRealityConfigured(false) ||
                !MixedRealityToolkit.Instance.ActiveProfile.IsInputSystemEnabled ||
                 MixedRealityToolkit.Instance.ActiveProfile.InputSystemProfile.InputActionsProfile == null)
            {
                return;
            }

            inputActionRulesDigital = serializedObject.FindProperty("inputActionRulesDigital");
            inputActionRulesSingleAxis = serializedObject.FindProperty("inputActionRulesSingleAxis");
            inputActionRulesDualAxis = serializedObject.FindProperty("inputActionRulesDualAxis");
            inputActionRulesVectorAxis = serializedObject.FindProperty("inputActionRulesVectorAxis");
            inputActionRulesQuaternionAxis = serializedObject.FindProperty("inputActionRulesQuaternionAxis");
            inputActionRulesPoseAxis = serializedObject.FindProperty("inputActionRulesPoseAxis");

            baseActionLabels = MixedRealityToolkit.Instance.ActiveProfile.InputSystemProfile.InputActionsProfile.InputActions
                .Where(action => action.AxisConstraint != AxisType.None && action.AxisConstraint != AxisType.Raw)
                .Select(action => action.Description)
                .ToArray();

            baseActionIds = MixedRealityToolkit.Instance.ActiveProfile.InputSystemProfile.InputActionsProfile.InputActions
                .Where(action => action.AxisConstraint != AxisType.None && action.AxisConstraint != AxisType.Raw)
                .Select(action => (int)action.Id)
                .ToArray();

            thisProfile = target as MixedRealityInputActionRulesProfile;

            ResetCriteria();
        }

        public override void OnInspectorGUI()
        {
            RenderMixedRealityToolkitLogo();

            if (!MixedRealityInspectorUtility.CheckMixedRealityConfigured())
            {
                return;
            }

            if (!MixedRealityToolkit.Instance.ActiveProfile.IsInputSystemEnabled)
            {
                EditorGUILayout.HelpBox("No input system is enabled, or you need to specify the type in the main configuration profile.", MessageType.Error);

                DrawBacktrackProfileButton("Back to Configuration Profile", MixedRealityToolkit.Instance.ActiveProfile);

                return;
            }

            if (MixedRealityToolkit.Instance.ActiveProfile.InputSystemProfile.InputActionsProfile == null)
            {
                EditorGUILayout.HelpBox("No Input Actions profile was specified.", MessageType.Error);

                DrawBacktrackProfileButton("Back to Input Profile", MixedRealityToolkit.Instance.ActiveProfile.InputSystemProfile);

                return;
            }

            if (DrawBacktrackProfileButton("Back to Input Profile", MixedRealityToolkit.Instance.ActiveProfile.InputSystemProfile))
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Input Action Rules Profile", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Input Action Rules help define alternative Actions that will be raised based on specific criteria.\n\n" +
                                    "You can create new rules by assigning a base Input Action below, then assigning the criteria you'd like to meet. When the criteria is met, the Rule's Action will be raised with the criteria value.\n\n" +
                                    "Note: Rules can only be created for the same axis constraints.", MessageType.Info);

            EditorGUILayout.Space();

            CheckProfileLock(target);

            var isGuiLocked = !(MixedRealityPreferences.LockProfiles && !((BaseMixedRealityProfile)target).IsCustomProfile);
            GUI.enabled = isGuiLocked;

            serializedObject.Update();

            selectedBaseActionId = RenderBaseInputAction(selectedBaseActionId, out currentBaseAction);
            GUI.enabled = isGuiLocked && currentBaseAction != MixedRealityInputAction.None;
            RenderCriteriaField(currentBaseAction);

            if (selectedBaseActionId == selectedRuleActionId)
            {
                selectedRuleActionId = 0;
            }

            selectedRuleActionId = RenderRuleInputAction(selectedRuleActionId, out currentRuleAction);

            EditorGUILayout.Space();

            GUI.enabled = isGuiLocked &&
                          !RuleExists() &&
                          currentBaseAction != MixedRealityInputAction.None &&
                          currentRuleAction != MixedRealityInputAction.None &&
                          currentBaseAction.AxisConstraint != AxisType.None &&
                          currentBaseAction.AxisConstraint != AxisType.Raw;

            if (GUILayout.Button(RuleAddButtonContent, EditorStyles.miniButton))
            {
                AddRule();
                ResetCriteria();
            }

            GUI.enabled = isGuiLocked;

            EditorGUILayout.Space();

            var isWideMode = EditorGUIUtility.wideMode;
            EditorGUIUtility.wideMode = true;

            RenderList(inputActionRulesDigital, digitalFoldouts);
            RenderList(inputActionRulesSingleAxis, singleAxisFoldouts);
            RenderList(inputActionRulesDualAxis, dualAxisFoldouts);
            RenderList(inputActionRulesVectorAxis, vectorFoldouts);
            RenderList(inputActionRulesQuaternionAxis, quaternionFoldouts);
            RenderList(inputActionRulesPoseAxis, poseFoldouts);

            EditorGUIUtility.wideMode = isWideMode;
            serializedObject.ApplyModifiedProperties();
        }

        private bool RuleExists()
        {
            switch (currentBaseAction.AxisConstraint)
            {
                default:
                    return false;
                case AxisType.Digital:
                    return thisProfile.InputActionRulesDigital.Any(digitalRule => digitalRule.BaseAction == currentBaseAction && digitalRule.RuleAction == currentRuleAction && digitalRule.Criteria == currentBoolCriteria);
                case AxisType.SingleAxis:
                    return thisProfile.InputActionRulesSingleAxis.Any(singleAxisRule => singleAxisRule.BaseAction == currentBaseAction && singleAxisRule.RuleAction == currentRuleAction && singleAxisRule.Criteria.Equals(currentSingleAxisCriteria));
                case AxisType.DualAxis:
                    return thisProfile.InputActionRulesDualAxis.Any(dualAxisRule => dualAxisRule.BaseAction == currentBaseAction && dualAxisRule.RuleAction == currentRuleAction && dualAxisRule.Criteria == currentDualAxisCriteria);
                case AxisType.ThreeDofPosition:
                    return thisProfile.InputActionRulesVectorAxis.Any(vectorAxisRule => vectorAxisRule.BaseAction == currentBaseAction && vectorAxisRule.RuleAction == currentRuleAction && vectorAxisRule.Criteria == currentVectorCriteria);
                case AxisType.ThreeDofRotation:
                    return thisProfile.InputActionRulesQuaternionAxis.Any(quaternionRule => quaternionRule.BaseAction == currentBaseAction && quaternionRule.RuleAction == currentRuleAction && quaternionRule.Criteria == currentQuaternionCriteria);
                case AxisType.SixDof:
                    return thisProfile.InputActionRulesPoseAxis.Any(poseRule => poseRule.BaseAction == currentBaseAction && poseRule.RuleAction == currentRuleAction && poseRule.Criteria == currentPoseCriteria);
            }
        }

        private void ResetCriteria()
        {
            selectedBaseActionId = 0;
            selectedRuleActionId = 0;
            currentBaseAction = MixedRealityInputAction.None;
            currentRuleAction = MixedRealityInputAction.None;
            currentBoolCriteria = false;
            currentSingleAxisCriteria = 0f;
            currentDualAxisCriteria = Vector2.zero;
            currentVectorCriteria = Vector3.zero;
            currentQuaternionCriteria = Quaternion.identity;
            currentPoseCriteria = MixedRealityPose.ZeroIdentity;

            digitalFoldouts = new bool[inputActionRulesDigital.arraySize];
            singleAxisFoldouts = new bool[inputActionRulesSingleAxis.arraySize];
            dualAxisFoldouts = new bool[inputActionRulesDualAxis.arraySize];
            vectorFoldouts = new bool[inputActionRulesVectorAxis.arraySize];
            quaternionFoldouts = new bool[inputActionRulesQuaternionAxis.arraySize];
            poseFoldouts = new bool[inputActionRulesPoseAxis.arraySize];
        }

        private static void GetCompatibleActions(MixedRealityInputAction baseAction)
        {
            ruleActionLabels = MixedRealityToolkit.Instance.ActiveProfile.InputSystemProfile.InputActionsProfile.InputActions
                .Where(inputAction => inputAction.AxisConstraint == baseAction.AxisConstraint && inputAction.Id != baseAction.Id)
                .Select(action => action.Description)
                .ToArray();

            ruleActionIds = MixedRealityToolkit.Instance.ActiveProfile.InputSystemProfile.InputActionsProfile.InputActions
                .Where(inputAction => inputAction.AxisConstraint == baseAction.AxisConstraint && inputAction.Id != baseAction.Id)
                .Select(action => (int)action.Id)
                .ToArray();
        }

        private void RenderCriteriaField(MixedRealityInputAction action, SerializedProperty criteriaValue = null)
        {
            var isWideMode = EditorGUIUtility.wideMode;
            EditorGUIUtility.wideMode = true;
            if (action != MixedRealityInputAction.None)
            {
                switch (action.AxisConstraint)
                {
                    default:
                        EditorGUILayout.HelpBox("Base rule must have a valid axis constraint.", MessageType.Warning);
                        break;
                    case AxisType.Digital:
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(CriteriaContent, GUILayout.Width(128));
                        EditorGUI.BeginChangeCheck();
                        var boolValue = EditorGUILayout.Toggle(GUIContent.none, criteriaValue?.boolValue ?? currentBoolCriteria, GUILayout.Width(64), GUILayout.ExpandWidth(true));

                        if (EditorGUI.EndChangeCheck())
                        {
                            if (criteriaValue != null)
                            {
                                criteriaValue.boolValue = boolValue;
                            }
                            else
                            {
                                currentBoolCriteria = boolValue;
                            }
                        }

                        EditorGUILayout.EndHorizontal();
                        break;
                    case AxisType.SingleAxis:
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(CriteriaContent, GUILayout.Width(128));
                        EditorGUI.BeginChangeCheck();
                        var floatValue = EditorGUILayout.FloatField(GUIContent.none, criteriaValue?.floatValue ?? currentSingleAxisCriteria, GUILayout.Width(64), GUILayout.ExpandWidth(true));

                        if (EditorGUI.EndChangeCheck())
                        {
                            if (criteriaValue != null)
                            {
                                criteriaValue.floatValue = floatValue;
                            }
                            else
                            {
                                currentSingleAxisCriteria = floatValue;
                            }
                        }

                        EditorGUILayout.EndHorizontal();
                        break;
                    case AxisType.DualAxis:
                        EditorGUILayout.LabelField(CriteriaContent, GUILayout.Width(128));
                        EditorGUI.indentLevel++;
                        EditorGUI.BeginChangeCheck();
                        var dualAxisValue = EditorGUILayout.Vector2Field("Position", criteriaValue?.vector2Value ?? currentDualAxisCriteria, GUILayout.Width(64), GUILayout.ExpandWidth(true));

                        if (EditorGUI.EndChangeCheck())
                        {
                            if (criteriaValue != null)
                            {
                                criteriaValue.vector2Value = dualAxisValue;
                            }
                            else
                            {
                                currentDualAxisCriteria = dualAxisValue;
                            }
                        }

                        EditorGUI.indentLevel--;
                        break;
                    case AxisType.ThreeDofPosition:
                        EditorGUILayout.LabelField(CriteriaContent, GUILayout.Width(128));
                        EditorGUI.indentLevel++;
                        EditorGUI.BeginChangeCheck();
                        var positionValue = EditorGUILayout.Vector3Field("Position", criteriaValue?.vector3Value ?? currentVectorCriteria, GUILayout.ExpandWidth(true));

                        if (EditorGUI.EndChangeCheck())
                        {
                            if (criteriaValue != null)
                            {
                                criteriaValue.vector3Value = positionValue;
                            }
                            else
                            {
                                currentVectorCriteria = positionValue;
                            }
                        }

                        EditorGUI.indentLevel--;
                        break;
                    case AxisType.ThreeDofRotation:
                        EditorGUILayout.LabelField(CriteriaContent, GUILayout.Width(128));
                        EditorGUI.indentLevel++;
                        EditorGUI.BeginChangeCheck();
                        var rotationValue = EditorGUILayout.Vector3Field("Rotation", criteriaValue?.quaternionValue.eulerAngles ?? currentQuaternionCriteria.eulerAngles, GUILayout.ExpandWidth(true));

                        if (EditorGUI.EndChangeCheck())
                        {
                            if (criteriaValue != null)
                            {
                                criteriaValue.quaternionValue = Quaternion.Euler(rotationValue);
                            }
                            else
                            {
                                currentQuaternionCriteria = Quaternion.Euler(rotationValue);
                            }
                        }

                        EditorGUI.indentLevel--;
                        break;
                    case AxisType.SixDof:
                        EditorGUILayout.LabelField(CriteriaContent, GUILayout.Width(128));
                        EditorGUI.indentLevel++;

                        var posePosition = currentPoseCriteria.Position;
                        var poseRotation = currentPoseCriteria.Rotation;

                        if (criteriaValue != null)
                        {
                            posePosition = criteriaValue.FindPropertyRelative("position").vector3Value;
                            poseRotation = criteriaValue.FindPropertyRelative("rotation").quaternionValue;
                        }

                        EditorGUI.BeginChangeCheck();
                        posePosition = EditorGUILayout.Vector3Field("Position", posePosition);

                        poseRotation.eulerAngles = EditorGUILayout.Vector3Field("Rotation", poseRotation.eulerAngles);

                        if (EditorGUI.EndChangeCheck())
                        {
                            if (criteriaValue != null)
                            {
                                criteriaValue.FindPropertyRelative("position").vector3Value = posePosition;
                                criteriaValue.FindPropertyRelative("rotation").quaternionValue = poseRotation;
                            }
                            else
                            {
                                currentPoseCriteria.Position = posePosition;
                                currentPoseCriteria.Rotation = poseRotation;
                            }
                        }

                        EditorGUI.indentLevel--;
                        break;
                }

                EditorGUIUtility.wideMode = isWideMode;
            }
        }

        private void AddRule()
        {
            SerializedProperty rule;
            switch (currentBaseAction.AxisConstraint)
            {
                case AxisType.Digital:
                    inputActionRulesDigital.arraySize += 1;
                    rule = inputActionRulesDigital.GetArrayElementAtIndex(inputActionRulesDigital.arraySize - 1);
                    rule.FindPropertyRelative("criteria").boolValue = currentBoolCriteria;
                    break;
                case AxisType.SingleAxis:
                    inputActionRulesSingleAxis.arraySize += 1;
                    rule = inputActionRulesSingleAxis.GetArrayElementAtIndex(inputActionRulesSingleAxis.arraySize - 1);
                    rule.FindPropertyRelative("criteria").floatValue = currentSingleAxisCriteria;
                    break;
                case AxisType.DualAxis:
                    inputActionRulesDualAxis.arraySize += 1;
                    rule = inputActionRulesDualAxis.GetArrayElementAtIndex(inputActionRulesDualAxis.arraySize - 1);
                    rule.FindPropertyRelative("criteria").vector2Value = currentDualAxisCriteria;
                    break;
                case AxisType.ThreeDofPosition:
                    inputActionRulesVectorAxis.arraySize += 1;
                    rule = inputActionRulesVectorAxis.GetArrayElementAtIndex(inputActionRulesVectorAxis.arraySize - 1);
                    rule.FindPropertyRelative("criteria").vector3Value = currentVectorCriteria;
                    break;
                case AxisType.ThreeDofRotation:
                    inputActionRulesQuaternionAxis.arraySize += 1;
                    rule = inputActionRulesQuaternionAxis.GetArrayElementAtIndex(inputActionRulesQuaternionAxis.arraySize - 1);
                    rule.FindPropertyRelative("criteria").quaternionValue = currentQuaternionCriteria;
                    break;
                case AxisType.SixDof:
                    inputActionRulesPoseAxis.arraySize += 1;
                    rule = inputActionRulesPoseAxis.GetArrayElementAtIndex(inputActionRulesPoseAxis.arraySize - 1);
                    var criteria = rule.FindPropertyRelative("criteria");
                    criteria.FindPropertyRelative("position").vector3Value = currentPoseCriteria.Position;
                    criteria.FindPropertyRelative("rotation").quaternionValue = currentPoseCriteria.Rotation;
                    break;
                default:
                    Debug.LogError("Invalid Axis Constraint!");
                    return;
            }

            var baseAction = rule.FindPropertyRelative("baseAction");
            var baseActionId = baseAction.FindPropertyRelative("id");
            var baseActionDescription = baseAction.FindPropertyRelative("description");
            var baseActionConstraint = baseAction.FindPropertyRelative("axisConstraint");

            baseActionId.intValue = (int)currentBaseAction.Id;
            baseActionDescription.stringValue = currentBaseAction.Description;
            baseActionConstraint.intValue = (int)currentBaseAction.AxisConstraint;

            var ruleAction = rule.FindPropertyRelative("ruleAction");
            var ruleActionId = ruleAction.FindPropertyRelative("id");
            var ruleActionDescription = ruleAction.FindPropertyRelative("description");
            var ruleActionConstraint = ruleAction.FindPropertyRelative("axisConstraint");

            ruleActionId.intValue = (int)currentRuleAction.Id;
            ruleActionDescription.stringValue = currentRuleAction.Description;
            ruleActionConstraint.intValue = (int)currentRuleAction.AxisConstraint;
        }

        private int RenderBaseInputAction(int baseActionId, out MixedRealityInputAction action, bool isLocked = false)
        {
            action = MixedRealityInputAction.None;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(BaseActionContent, GUILayout.Width(128));
            EditorGUI.BeginChangeCheck();

            if (!isLocked)
            {
                baseActionId = EditorGUILayout.IntPopup(baseActionId, baseActionLabels, baseActionIds, GUILayout.ExpandWidth(true));
            }

            for (int i = 0; i < MixedRealityToolkit.Instance.ActiveProfile.InputSystemProfile.InputActionsProfile.InputActions.Length; i++)
            {
                if (baseActionId == (int)MixedRealityToolkit.Instance.ActiveProfile.InputSystemProfile.InputActionsProfile.InputActions[i].Id)
                {
                    action = MixedRealityToolkit.Instance.ActiveProfile.InputSystemProfile.InputActionsProfile.InputActions[i];
                }
            }

            if (action != MixedRealityInputAction.None)
            {
                GetCompatibleActions(action);
            }

            if (isLocked)
            {
                EditorGUILayout.LabelField(action.Description, EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
            }

            EditorGUILayout.EndHorizontal();
            return baseActionId;
        }

        private int RenderRuleInputAction(int ruleActionId, out MixedRealityInputAction action)
        {
            action = MixedRealityInputAction.None;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(RuleActionContent, GUILayout.Width(128));
            EditorGUI.BeginChangeCheck();
            ruleActionId = EditorGUILayout.IntPopup(ruleActionId, ruleActionLabels, ruleActionIds, GUILayout.ExpandWidth(true));

            for (int i = 0; i < MixedRealityToolkit.Instance.ActiveProfile.InputSystemProfile.InputActionsProfile.InputActions.Length; i++)
            {
                if (ruleActionId == (int)MixedRealityToolkit.Instance.ActiveProfile.InputSystemProfile.InputActionsProfile.InputActions[i].Id)
                {
                    action = MixedRealityToolkit.Instance.ActiveProfile.InputSystemProfile.InputActionsProfile.InputActions[i];
                }
            }

            EditorGUILayout.EndHorizontal();
            return ruleActionId;
        }

        private void RenderList(SerializedProperty list, bool[] foldouts)
        {
            for (int i = 0; i < list?.arraySize; i++)
            {
                var rule = list.GetArrayElementAtIndex(i);
                var criteria = rule.FindPropertyRelative("criteria");

                var baseAction = rule.FindPropertyRelative("baseAction");
                var baseActionId = baseAction.FindPropertyRelative("id");
                var baseActionDescription = baseAction.FindPropertyRelative("description");
                var baseActionConstraint = baseAction.FindPropertyRelative("axisConstraint");

                var ruleAction = rule.FindPropertyRelative("ruleAction");
                var ruleActionId = ruleAction.FindPropertyRelative("id");
                var ruleActionDescription = ruleAction.FindPropertyRelative("description");
                var ruleActionConstraint = ruleAction.FindPropertyRelative("axisConstraint");

                EditorGUILayout.BeginHorizontal();
                foldouts[i] = EditorGUILayout.Foldout(foldouts[i], new GUIContent($"{baseActionDescription.stringValue} -> {ruleActionDescription.stringValue}"), true);

                if (GUILayout.Button(RuleMinusButtonContent, EditorStyles.miniButtonRight, GUILayout.Width(24f)))
                {
                    list.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndHorizontal();
                    return;
                }

                EditorGUILayout.EndHorizontal();

                if (foldouts[i])
                {
                    EditorGUI.indentLevel++;

                    MixedRealityInputAction newBaseAction;
                    baseActionId.intValue = RenderBaseInputAction(baseActionId.intValue, out newBaseAction, true);
                    baseActionDescription.stringValue = newBaseAction.Description;
                    baseActionConstraint.intValue = (int)newBaseAction.AxisConstraint;

                    if (baseActionId.intValue == ruleActionId.intValue || newBaseAction == MixedRealityInputAction.None || baseActionConstraint.intValue != ruleActionConstraint.intValue)
                    {
                        criteria.Reset();
                        ruleActionId.intValue = (int)MixedRealityInputAction.None.Id;
                        ruleActionDescription.stringValue = MixedRealityInputAction.None.Description;
                        ruleActionConstraint.intValue = (int)MixedRealityInputAction.None.AxisConstraint;
                    }

                    RenderCriteriaField(newBaseAction, criteria);

                    MixedRealityInputAction newRuleAction;
                    ruleActionId.intValue = RenderRuleInputAction(ruleActionId.intValue, out newRuleAction);
                    ruleActionDescription.stringValue = newRuleAction.Description;
                    ruleActionConstraint.intValue = (int)newRuleAction.AxisConstraint;
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space();
            }
        }
    }
}