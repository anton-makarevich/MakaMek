using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Sanet.MakaMek.Core.Models.Units;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media.Immutable;
using AvaloniaAnimation = Avalonia.Animation.Animation;
using MonacoLabs.Presto;
using Sanet.MakaMek.Avalonia.Controls.Services;
using Sanet.MakaMek.Avalonia.Converters;
using Sanet.MakaMek.Avalonia.Services;
using Sanet.MakaMek.Avalonia.Utils;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Events;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.Models.Movement;
using Sanet.MakaMek.Presentation.UiStates;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Sanet.MakaMek.Services;

namespace Sanet.MakaMek.Avalonia.Controls
{
    public class UnitControl : Grid, IDisposable
    {
        private readonly Image _unitImage;
        private readonly IImageService<Bitmap> _imageService;
        private readonly BattleMapViewModel _viewModel;
        private readonly IUnit _unit;
        private readonly IDisposable _subscription;
        private readonly IDisposable? _moveSubscription;
        private readonly IDisposable? _interruptionSubscription;
        private readonly IUnitSpriteService<UnitSpriteSet>? _spriteService;
        private readonly ControlScope _playbackScope;
        private readonly Queue<MoveUnitCommand> _pendingMoves = new();
        private readonly ImageBrush _tintMaskBrush = new();
        private Bitmap? _staticUnitImage;
        private UnitSpriteSet? _spriteSet;
        private MovementPlaybackTimeline? _activeTimeline;
        private MonacoLabs.Presto.Timer? _playbackTimer;
        private PlaybackPoint? _displayPosition;
        private double? _displayFacingDegrees;
        private bool _isPlaying;
        private bool _disposed;
        private readonly Border _tintBorder;
        private readonly StackPanel _actionButtons;
        private readonly StackPanel _healthBars;
        private readonly ProgressBar _armorBar;
        private readonly ProgressBar _structureBar;
        private readonly ProgressBar _heatBar;
        private readonly StackPanel _eventsPanel;
        private readonly TimeSpan _eventDisplayDuration = TimeSpan.FromSeconds(5);
        private readonly List<PathSegmentControl> _unitMovementPathSegments = [];

        // The hexagonal outline that marks the selected unit, and the motion that plays it.
        private readonly SelectionHex _selectionHex;

        private readonly IAvaloniaResourcesLocator _resourcesLocator = new AvaloniaResourcesLocator();

        // Get the contrasting foreground converter from resources
        private readonly ContrastingForegroundConverter? _contrastingForegroundConverter;

        public UnitControl(IUnit unit, IImageService<Bitmap> imageService, BattleMapViewModel viewModel,
            IUnitSpriteService<UnitSpriteSet>? spriteService = null)
        {
            _unit = unit;
            _imageService = imageService;
            _viewModel = viewModel;
            _spriteService = spriteService;

            // Every playback timer lives in this scope, so it stops when the control leaves the tree.
            _playbackScope = PrestoAvalonia.CreateScope(this);

            _contrastingForegroundConverter = _resourcesLocator
                .TryFindResource("ContrastingForegroundConverter") as ContrastingForegroundConverter;

            IsHitTestVisible = false;
            Width = HexCoordinatesPixelExtensions.HexWidth;
            Height = HexCoordinatesPixelExtensions.HexHeight;

            _unitImage = new Image
            {
                Width = Width * 0.84,
                Height = Height * 0.84,
                Stretch = Stretch.Fill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _tintBorder = new Border
            {
                Width = _unitImage.Width,
                Height = _unitImage.Height,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Colors.White),
                Opacity = 0.7
            };

            _actionButtons = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsVisible = false,
                IsHitTestVisible = false,
                Spacing = 4,
                Margin = new Thickness(4)
            };

            // Create a health bars panel
            _healthBars = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsVisible = true,
                IsHitTestVisible = false,
                Spacing = 2,
                Margin = new Thickness(4),
                Width = Width * 0.8
            };

            // Create an events panel for damage labels
            _eventsPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsVisible = true,
                IsHitTestVisible = false,
                Spacing = 4,
                Margin = new Thickness(4)
            };

            // Get colors from resources with fallbacks
            var armorBrush = _resourcesLocator.TryFindResource("MechArmorBrush") as SolidColorBrush
                             ?? new SolidColorBrush(Colors.LightBlue);
            var structureBrush = _resourcesLocator.TryFindResource("MechStructureBrush") as SolidColorBrush
                                 ?? new SolidColorBrush(Colors.Orange);
            var backgroundBrush = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0));
            var heatBrush = _resourcesLocator.TryFindResource("HeatBrush") as SolidColorBrush
                           ?? new SolidColorBrush(Colors.Red);

            // Create an armor bar
            _armorBar = new ProgressBar
            {
                Foreground = armorBrush,
                Background = backgroundBrush,
                Height = 6,
                MinWidth = 0,
                Width = Width * 0.8,
                CornerRadius = new CornerRadius(3),
                Minimum = 0,
                Maximum = 1,
                Value = 1
            };

            // Create a structure bar 
            _structureBar = new ProgressBar
            {
                Foreground = structureBrush,
                Background = backgroundBrush,
                Height = 6,
                MinWidth = 0,
                Width = Width * 0.8,
                CornerRadius = new CornerRadius(3),
                Minimum = 0,
                Maximum = 1,
                Value = 1
            };

            // Create a heat level bar (vertical)
            _heatBar = new ProgressBar
            {
                Foreground = heatBrush,
                Background = backgroundBrush,
                Width = 6,
                MinHeight = 0,
                Height = Height * 0.8,
                Orientation = Orientation.Vertical,
                CornerRadius = new CornerRadius(3),
                Minimum = 0,
                Maximum = MaxHeatLevel, 
                Value = 0,
            };

            // Add bars to the panel
            _healthBars.Children.Add(_armorBar);
            _healthBars.Children.Add(_structureBar);

            var color = _unit.Owner != null
                ? Color.Parse(_unit.Owner.Tint)
                : Colors.Yellow;

            // The selection outline marks the unit and owns the motion that plays it.
            _selectionHex = new SelectionHex(
                new SolidColorBrush(color),
                _resourcesLocator.TryFindResource("TargetingBrush") as IBrush
                ?? new SolidColorBrush(Colors.IndianRed));

            // Create torso direction indicator
            var torsoArrow = new Path
            {
                Data = new StreamGeometry(),
                Stroke = new SolidColorBrush(Colors.Black),
                StrokeThickness = 1,
                Fill = new SolidColorBrush(color),
                Opacity = 0.8,
                IsVisible = false,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = Width * 0.2,
                Height = Height * 0.2
            };

            // Create arrow geometry using the same style as PathSegmentControl
            var arrowSize = torsoArrow.Width;
            using (var context = ((StreamGeometry)torsoArrow.Data).Open())
            {
                var arrowEndPoint = new Point(arrowSize * 0.5, -arrowSize);
                var leftPoint = new Point(0, 0);
                var rightPoint = new Point(arrowSize, 0);

                context.BeginFigure(arrowEndPoint, true);
                context.LineTo(leftPoint);
                context.LineTo(rightPoint);
                context.LineTo(arrowEndPoint);
                context.EndFigure(true);
                context.SetFillRule(FillRule.NonZero);
            }

            // Create a destroyed indicator (cross)
            var destroyedCross = new Path
            {
                Data = new StreamGeometry(),
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 4,
                Opacity = 0.8,
                IsVisible = false,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = Width * 0.3,
                Height = Height * 0.3
            };
            
            // Draw cross-geometry
            var crossSize = destroyedCross.Width;
            using (var context = ((StreamGeometry)destroyedCross.Data).Open())
            {
                // Diagonal line 1
                context.BeginFigure(new Point(0, 0), true);
                context.LineTo(new Point(crossSize, crossSize));
                // Diagonal line 2
                context.BeginFigure(new Point(crossSize, 0), true);
                context.LineTo(new Point(0, crossSize));
            }

            // Create status indicators panel
            var statusPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                IsVisible = false,
                Margin = new Thickness(0, 0, 0, 5),
                Spacing = 2
            };

            // Create prone indicator (circular)
            var proneIndicator = CreateStatusIndicator("PR", color);

            // Create immobile indicator (circular)
            var immobileIndicator = CreateStatusIndicator("IM", color);

            // Add indicators to status panel
            statusPanel.Children.Add(proneIndicator);
            statusPanel.Children.Add(immobileIndicator);

            Children.Add(_selectionHex);
            Children.Add(_unitImage);
            Children.Add(_tintBorder);
            Children.Add(torsoArrow);
            Children.Add(destroyedCross);
            Children.Add(statusPanel);

            // Create an observable that polls the unit's position and selection state
            _subscription = Observable
                .Interval(TimeSpan.FromMilliseconds(32)) // ~60fps
                .Select(_ => new UnitState()
                {
                    Position = _unit.Position,
                    IsDeployed = _unit.IsDeployed,
                    SelectedUnit = viewModel.SelectedUnit,
                    SelectionKind = UnitSelectionKindResolver.Resolve(_unit, viewModel.SelectedUnit, viewModel.CurrentState),
                    Actions = viewModel.CurrentState.GetAvailableActions(),
                    IsWeaponsPhase = viewModel.CurrentState is WeaponsAttackState,
                    TorsoDirection = (_unit as Mech)?.TorsoDirection,
                    TotalMaxArmor = _unit.TotalMaxArmor,
                    TotalCurrentArmor = _unit.TotalCurrentArmor,
                    TotalMaxStructure = _unit.TotalMaxStructure,
                    TotalCurrentStructure = _unit.TotalCurrentStructure,
                    Status = _unit.Status,
                    Events = _unit.Notifications,
                    CurrentHeat = _unit.CurrentHeat,
                    MovementTaken = _unit.MovementTaken
                })
                .DistinctUntilChanged()
                .ObserveOn(SynchronizationContext.Current) // Ensure events are processed on the UI thread
                .Subscribe(state =>
                {
                    // Phase 1: Update state-based visuals (independent of positioning)
                    UpdateDestroyedState(destroyedCross);
                    
                    if (state.Position == null) return; // unit is not deployed, no need to display

                    UpdateStatusIndicators(statusPanel, proneIndicator, immobileIndicator);
                    _selectionHex.Update(state.SelectionKind);
                    UpdateRotation(state);
                    
                    // Phase 2: Process events (modifies _eventsPanel.Children before positioning)
                    ProcessEvents(state.Events);
                    
                    // Phase 3: Position all UI elements (depends on final _eventsPanel.Children.Count)
                    Render();
                    
                    // Phase 4: Update values and content (depends on elements being positioned)
                    UpdateActionButtons(state.Actions);
                    UpdateHealthBars(state.TotalCurrentArmor, state.TotalMaxArmor, state.TotalCurrentStructure,
                        state.TotalMaxStructure);
                    UpdateHeatBar(state.CurrentHeat);
                    UpdateMovementPathDisplay(state);
                    UpdateTorsoArrow(state, torsoArrow);
                });

            // Movement commands arrive on the UI dispatcher from the view model. They never write unit
            // state: the model already holds the destination, and this control only replays the path.
            _moveSubscription = viewModel.UnitMoveCommands
                .Where(command => command.UnitId == _unit.Id)
                .Subscribe(OnMoveCommand);

            _interruptionSubscription = viewModel.UnitPlaybackInterruptions
                .Where(interruption => interruption.IsResetAll || interruption.UnitId == _unit.Id)
                .Subscribe(_ => CancelPlayback());

            // Initial update
            Render();
            UpdateImage().SafeFireAndForget();
            UpdateHealthBars(_unit.TotalCurrentArmor, _unit.TotalMaxArmor, _unit.TotalCurrentStructure,
                _unit.TotalMaxStructure);
            UpdateHeatBar(_unit.CurrentHeat);
        }

        private const double MaxHeatLevel = 30;

        private void UpdateHealthBars(int currentArmor, int maxArmor, int currentStructure, int maxStructure)
        {
            // Update armor bar
            _armorBar.Maximum = maxArmor;
            _armorBar.Value = currentArmor;

            // Update structure bar
            _structureBar.Maximum = maxStructure;
            _structureBar.Value = currentStructure;
        }

        private void UpdateHeatBar(int currentHeat)
        {
            _heatBar.Value = currentHeat;
            _heatBar.IsVisible = currentHeat > 0;
        }

        private void UpdateActionButtons(IEnumerable<StateAction> actions)
        {
            _actionButtons.Children.Clear();
            _actionButtons.IsVisible = false;

            // On mobile, action buttons are rendered in a fixed overlay in BattleMapView
            if (_viewModel.AreActionsMenuOffMap) return;

            var activeUnit = _viewModel.CurrentState is WeaponsAttackState state
                ? state.Attacker
                : _viewModel.SelectedUnit;

            if (activeUnit != _unit) return;

            foreach (var action in actions)
            {
                if (!action.IsVisible) continue;

                var button = new Button
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Content = action.Label,
                    IsEnabled = action.IsEnabled
                };
                button.Classes.Add("actionButtonText");
                if (action.DisabledReason is { } disabledReason)
                    ToolTip.SetTip(button, disabledReason);

                button.Click += (_, _) =>
                {
                    action.OnExecute();
                    _actionButtons.IsVisible = false;
                };

                _actionButtons.Children.Add(button);
                _actionButtons.IsVisible = true;
            }
        }

        private void Render()
        {
            IsVisible = _unit.IsDeployed;

            // While a command is playing back, the display position wins. The model already holds the
            // destination, and the poll must not snap the control there before the animation finishes.
            if (_displayPosition is { } display)
            {
                PositionAttachedElements(display.X, display.Y);
                return;
            }

            if (_unit.Position == null) return;
            var hexPosition = _unit.Position;
            PositionAttachedElements(hexPosition.Coordinates.H, hexPosition.Coordinates.V);
        }

        private void PositionAttachedElements(double leftPos, double topPos)
        {
            SetValue(Canvas.LeftProperty, leftPos);
            SetValue(Canvas.TopProperty, topPos);

            // Update buttons position to follow the unit
            if (_actionButtons.Parent == null && Parent is Canvas canvas)
            {
                canvas.Children.Add(_actionButtons);
            }

            Canvas.SetLeft(_actionButtons, leftPos);
            Canvas.SetTop(_actionButtons, topPos + Height);

            // Update health bars position to follow the unit
            if (_healthBars.Parent == null && Parent is Canvas canvas2)
            {
                canvas2.Children.Add(_healthBars);
                canvas2.Children.Add(_heatBar);
            }

            Canvas.SetLeft(_healthBars, leftPos);
            Canvas.SetTop(_healthBars, topPos - 15); // Position above the unit
            Canvas.SetLeft(_heatBar, leftPos);
            Canvas.SetTop(_heatBar, topPos); 

            // Update events panel position to follow the unit
            if (_eventsPanel.Parent == null && Parent is Canvas canvas3)
            {
                canvas3.Children.Add(_eventsPanel);
            }

            Canvas.SetLeft(_eventsPanel, leftPos);
            Canvas.SetTop(_eventsPanel, topPos - 40 - _eventsPanel.Children.Count * 10);

            if (Parent is not Canvas canvas4) return;
            if (_actionButtons.ZIndex < canvas4.Children.Count)
                _actionButtons.ZIndex = canvas4.Children.Count;
        }

        /// <summary>
        /// Processes events from the unit and creates UI elements to display them
        /// </summary>
        /// <param name="events">The events to process</param>
        private void ProcessEvents(IReadOnlyCollection<UiEvent> events)
        {
            if (events.Count == 0) return;

            // Process all events in the queue
            while (_unit.DequeueNotification() is { } uiEvent)
            {
                var template = _viewModel.LocalizationService.GetString($"Events_Unit_{uiEvent.Type}");
                var damageText = string.Format(template, uiEvent.Parameters);
                CreateDamageLabel(damageText, GetEventLabelBackground(uiEvent.Type));
            }
        }

        private IBrush GetEventLabelBackground(UiEventType uiEventType)
        {
            return uiEventType switch
            {
                UiEventType.ArmorDamage => _resourcesLocator.TryFindResource("MechArmorBrush") as SolidColorBrush
                                           ?? new SolidColorBrush(Colors.LightBlue),
                UiEventType.StructureDamage =>
                    _resourcesLocator.TryFindResource("MechStructureBrush") as SolidColorBrush
                    ?? new SolidColorBrush(Colors.Orange),
                _ => _resourcesLocator.TryFindResource("DestroyedBrush") as SolidColorBrush
                     ?? new SolidColorBrush(Colors.Red)
            };
        }

        /// <summary>
        /// Creates a damage label with animation
        /// </summary>
        /// <param name="damageText">The damage text to display</param>
        /// <param name="background">Label background depends on the event type</param>
        private void CreateDamageLabel(string damageText, IBrush background)
        {
            // Create a border with text
            var textBlock = new TextBlock
            {
                Text = damageText,
                Foreground = new ImmutableSolidColorBrush(Colors.White),
                FontWeight = FontWeight.Bold,
                Background = background,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Add to the events panel
            _eventsPanel.Children.Add(textBlock);

            // Start animation
            AnimateDamageLabel(textBlock).SafeFireAndForget();
        }

        /// <summary>
        /// Animates a damage label using keyframe animation and removes it when done
        /// </summary>
        /// <param name="label">The label to animate</param>
        private async Task AnimateDamageLabel(TextBlock label)
        {
            // Get the animation from resources
            if (_resourcesLocator.TryFindResource("DamageLabelAnimation") is AvaloniaAnimation animation)
            {
                // Start the animation and await its completion
                await animation.RunAsync(label);
            }
            else
            {
                // Fallback in case the animation resource isn't found
                await Task.Delay(_eventDisplayDuration);
            }

            // Remove the label from the panel
            _eventsPanel.Children.Remove(label);
        }

        private async Task UpdateImage()
        {
            var model = _unit.Model.ToUpperInvariant();
            var image = await _imageService.GetImage("units/mechs", model);

            if (_spriteService != null)
            {
                _spriteSet = await _spriteService.GetSpriteSet(model);
            }

            _staticUnitImage = image;

            if (_spriteSet is null)
            {
                // v1 units keep the historical 84% frame.
                _unitImage.Stretch = Stretch.Fill;
                _unitImage.Width = Width * 0.84;
                _unitImage.Height = Height * 0.84;
                _tintBorder.Width = _unitImage.Width;
                _tintBorder.Height = _unitImage.Height;
            }

            if (image != null && !_isPlaying)
            {
                ShowImage(image);
            }

            // Apply player's tint color if available
            if (_unit.Owner == null) return;
            var color = Color.Parse(_unit.Owner.Tint);
            _tintBorder.Background = new SolidColorBrush(color);
        }

        /// <summary>
        /// Shows an image in the unit slot and uses the same bitmap as the owner-tint mask, so the tint
        /// follows the sprite frame instead of staying on the idle art.
        /// </summary>
        private void ShowImage(IImage image)
        {
            _unitImage.Source = image;

            if (_spriteSet is not null && image is CroppedBitmap cropped)
            {
                // CroppedBitmap is not an IImageBrushSource, so the tint mask reads the same sub-rectangle
                // from the atlas the frame was cut from.
                var rect = cropped.SourceRect;
                _tintMaskBrush.Source = (IImageBrushSource)_spriteSet.Atlas;
                _tintMaskBrush.SourceRect =
                    new RelativeRect(rect.X, rect.Y, rect.Width, rect.Height, RelativeUnit.Absolute);
            }
            else
            {
                _tintMaskBrush.Source = (IImageBrushSource)image;
                _tintMaskBrush.SourceRect = new RelativeRect(0, 0, 1, 1, RelativeUnit.Relative);
            }

            _tintMaskBrush.Stretch = Stretch.Fill;
            _tintBorder.OpacityMask = _tintMaskBrush;

            // A unit with an atlas is drawn 1:1: the control box is sized to the bitmap so sprite pixels
            // equal board pixels and <c>distancePerCycle</c> can be compared with path distance directly.
            if (_spriteSet is not null)
            {
                var width = image.Size.Width;
                var height = image.Size.Height;
                _unitImage.Stretch = Stretch.Fill;
                _unitImage.Width = width;
                _unitImage.Height = height;
                _tintBorder.Width = width;
                _tintBorder.Height = height;
            }
        }

        private void ShowStaticImage()
        {
            if (_staticUnitImage != null)
            {
                ShowImage(_staticUnitImage);
            }
        }

        /// <summary>
        /// Queues a received movement command for this unit. Commands are replayed one after another so
        /// a chunk that continues after an interrupt (a fall, a bridge collapse) plays after the chunk
        /// that preceded it, each starting from its own path start.
        /// </summary>
        internal void OnMoveCommand(MoveUnitCommand command)
        {
            _pendingMoves.Enqueue(command);
            if (!_isPlaying)
            {
                StartNextPlayback();
            }
        }

        private void StartNextPlayback()
        {
            while (_pendingMoves.Count > 0)
            {
                var command = _pendingMoves.Dequeue();
                var timeline = MovementPlaybackTimeline.Create(command.MovementPath, command.MovementType);
                if (timeline is null) continue; // standing still or a no-op path: nothing to show

                StartPlayback(timeline);
                return;
            }
        }

        private void StartPlayback(MovementPlaybackTimeline timeline)
        {
            _activeTimeline = timeline;
            _isPlaying = true;

            // The model has already arrived; the presentation starts at the path start and follows the
            // same fixed-duration clock for position, facing and sprite frames.
            ApplyPlaybackSample(timeline.Sample(0));

            if (Presto.Defaults.ReducedMotion)
            {
                ApplyPlaybackSample(timeline.Sample(1));
                CompletePlayback();
                return;
            }

            _playbackTimer = _playbackScope.Execute(_ => Presto.CreateTimer(new TimerParams
            {
                Duration = timeline.DurationMilliseconds,
                Autoplay = true,
                OnUpdate = timer => ApplyPlaybackProgress(timer.Progress),
                OnComplete = _ => CompletePlayback()
            }));
        }

        private void ApplyPlaybackProgress(double progress)
        {
            if (_activeTimeline is null) return;
            ApplyPlaybackSample(_activeTimeline.Sample(progress));
        }

        private void ApplyPlaybackSample(MovementPlaybackSample sample)
        {
            _displayPosition = sample.Position;
            _displayFacingDegrees = sample.FacingDegrees;

            Render();
            RenderTransform = new RotateTransform(sample.FacingDegrees, 0, 0);
            UpdateSpriteFrame(sample);
        }

        private void UpdateSpriteFrame(MovementPlaybackSample sample)
        {
            if (_spriteSet is null) return;

            var clipName = sample.Clip switch
            {
                MovementPlaybackClip.Walk => "Walk",
                MovementPlaybackClip.Run => "Run",
                _ => null
            };

            if (clipName is null || !_spriteSet.Clips.TryGetValue(clipName, out var clip))
            {
                // Jump, prone, or a package without this clip: translate with the static art.
                ShowStaticImage();
                return;
            }

            var frameIndex = MovementPlaybackTimeline.ResolveFrameIndex(
                sample.SignedDistance, clip.DistancePerCycle, clip.Frames.Count);
            ShowImage(clip.Frames[frameIndex]);
        }

        private void CompletePlayback()
        {
            _playbackTimer = null;
            _activeTimeline = null;
            _isPlaying = false;
            RestoreAuthoritativeVisuals();

            if (_pendingMoves.Count > 0)
            {
                StartNextPlayback();
            }
        }

        /// <summary>
        /// Stops the active playback and drops anything queued: the model has moved in a way the
        /// animation cannot represent (displaced, fell, stood up, was destroyed, or the game changed).
        /// </summary>
        private void CancelPlayback()
        {
            _pendingMoves.Clear();

            var timer = _playbackTimer;
            _playbackTimer = null;
            timer?.Cancel();

            _activeTimeline = null;
            _isPlaying = false;
            RestoreAuthoritativeVisuals();
        }

        /// <summary>
        /// Returns the control to the authoritative model state after a playback: position, facing,
        /// static image and the sprite-frame fallback. Never writes display state back to the unit.
        /// </summary>
        private void RestoreAuthoritativeVisuals()
        {
            _displayPosition = null;
            _displayFacingDegrees = null;

            ShowStaticImage();
            Render();

            var facing = (_unit as Mech)?.TorsoDirection
                         ?? _unit.Position?.Facing
                         ?? HexDirection.Top;
            RenderTransform = new RotateTransform((int)facing * 60, 0, 0);
        }

        public bool HandleInteraction(Point position)
        {
            if (!_actionButtons.IsVisible)
                return false;

            // Find which button was clicked based on position
            var clickedButton = _actionButtons.Children
                .OfType<Button>()
                .FirstOrDefault(b => b.Bounds.Contains(position));

            if (clickedButton is not { IsEnabled: true, IsVisible: true }) return false;
            // Trigger the button's Click event
            clickedButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            return true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            CancelPlayback();
            HideMovementPath();
            _subscription.Dispose();
            _moveSubscription?.Dispose();
            _interruptionSubscription?.Dispose();
            _selectionHex.Dispose();
            _playbackScope.Revert();
        }

        public StackPanel ActionButtons => _actionButtons;

        private Border CreateStatusIndicator(string label, Color color)
        {
            return new Border
            {
                Width = 24,
                Height = 24,
                Background = new SolidColorBrush(color),
                CornerRadius = new CornerRadius(12), // Make it circular
                IsVisible = false,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = label.ToUpperInvariant(),
                    Foreground = _contrastingForegroundConverter != null
                        ? _contrastingForegroundConverter.Convert(_unit.Owner?.Tint, typeof(IBrush), null, null) as
                            IBrush
                        : new SolidColorBrush(Colors.White),
                    FontWeight = FontWeight.Bold,
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
        }

        private void ShowMovementPath(MovementPath path)
        {
            // Only update if the path has changed
            if (_unitMovementPathSegments.Count == path.Segments.Count)
            {
                return; // Path unchanged
            }

            HideMovementPath(); // Clear stale segments

            if (Parent is not Canvas canvas) return;

            var color = _unit.Owner != null
                ? Color.Parse(_unit.Owner.Tint)
                : Colors.Yellow;

            var zIndex = 0;
            foreach (var segment in path.Segments)
            {
                var segmentViewModel = new PathSegmentViewModel(segment);
                var segmentControl = new PathSegmentControl(segmentViewModel, color);
                _unitMovementPathSegments.Add(segmentControl);
                canvas.Children.Add(segmentControl);
                // Ensure path segments appear below the unit
                zIndex = segmentControl.ZIndex;
            }

            ZIndex = zIndex + 1;
        }

        private void HideMovementPath()
        {
            if (_unitMovementPathSegments.Count == 0) return;

            if (Parent is Canvas canvas)
            {
                foreach (var pathSegment in _unitMovementPathSegments)
                {
                    canvas.Children.Remove(pathSegment);
                }
            }

            _unitMovementPathSegments.Clear();
        }

        private void UpdateDestroyedState(Path destroyedCross)
        {
            destroyedCross.IsVisible = _unit.IsOutOfCommission;
            _healthBars.IsVisible = !_unit.IsDestroyed;
        }

        private void UpdateStatusIndicators(StackPanel statusPanel, Border proneIndicator, Border immobileIndicator)
        {
            if (_unit is Mech mech)
            {
                var isProne = mech is { IsProne: true, IsDestroyed: false };
                var isImmobile = mech is { IsImmobile: true, IsDestroyed: false };

                proneIndicator.IsVisible = isProne;
                immobileIndicator.IsVisible = isImmobile;
                statusPanel.IsVisible = isProne || isImmobile;
            }
            else
            {
                proneIndicator.IsVisible = false;
                immobileIndicator.IsVisible = false;
                statusPanel.IsVisible = false;
            }
        }

        private void UpdateRotation(UnitState state)
        {
            // Playback owns the body facing while it runs: the path, not the torso, decides where the
            // sprite points. The model-facing value is restored when the playback ends.
            if (_displayFacingDegrees is { } displayFacing)
            {
                RenderTransform = new RotateTransform(displayFacing, 0, 0);
                return;
            }

            var isMech = _unit is Mech;
            var torsoFacing = isMech && state.TorsoDirection.HasValue
                ? (int)state.TorsoDirection.Value
                : (int)state.Position!.Facing;

            // Rotate the entire control to show a torso/unit direction
            RenderTransform = new RotateTransform(torsoFacing * 60, 0, 0);
        }

        private void UpdateMovementPathDisplay(UnitState state)
        {
            // Show movement path when MovementTaken has cost
            if (state.MovementTaken is { TotalCost: > 0 })
            {
                ShowMovementPath(state.MovementTaken);
            }
            else
            {
                HideMovementPath();
            }
        }

        private void UpdateTorsoArrow(UnitState state, Path torsoArrow)
        {
            if (_unit is not Mech mech)
            {
                torsoArrow.IsVisible = false;
                return;
            }

            torsoArrow.IsVisible = state.IsWeaponsPhase
                                   && !_unit.IsDestroyed
                                   && state is { TorsoDirection: not null, Position: not null };
            
            if (!torsoArrow.IsVisible)
                return;

            // Calculate torso arrow rotation
            var torsoFacing = state.TorsoDirection.HasValue
                ? (int)state.TorsoDirection.Value
                : (int)state.Position!.Facing;
            var unitFacing = (int)state.Position!.Facing;
            
            // Since control is rotated to a torso direction, we need the opposite delta
            var deltaAngle = (unitFacing - torsoFacing + 6) % 6 * 60;
            torsoArrow.RenderTransform = new RotateTransform(deltaAngle);

            // Handle torso twist if it has been used
            if (state.IsWeaponsPhase && mech.HasUsedTorsoTwist && state.TorsoDirection.HasValue)
            {
                (_viewModel.CurrentState as WeaponsAttackState)?.HandleTorsoRotation(_unit.Id);
            }
        }
    }
}