using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media.Immutable;
using Sanet.MakaMek.Avalonia.Converters;
using Sanet.MakaMek.Avalonia.Services;
using Sanet.MakaMek.Avalonia.Utils;
using Sanet.MakaMek.Core.Events;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Presentation.UiStates;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Avalonia.Controls
{
    public class UnitControl : Grid, IDisposable
    {
        private readonly Image _unitImage;
        private readonly IImageService<Bitmap> _imageService;
        private readonly BattleMapViewModel _viewModel;
        private readonly IUnit _unit;
        private readonly IDisposable _subscription;
        private readonly Border _tintBorder;
        private readonly StackPanel _actionButtons;
        private readonly StackPanel _healthBars;
        private readonly ProgressBar _armorBar;
        private readonly ProgressBar _structureBar;
        private readonly ProgressBar _heatBar;
        private readonly StackPanel _eventsPanel;
        private readonly TimeSpan _eventDisplayDuration = TimeSpan.FromSeconds(5);
        private readonly List<PathSegmentControl> _unitMovementPathSegments = [];

        private readonly IAvaloniaResourcesLocator _resourcesLocator = new AvaloniaResourcesLocator();

        // Get the contrasting foreground converter from resources
        private readonly ContrastingForegroundConverter? _contrastingForegroundConverter;

        public UnitControl(IUnit unit, IImageService<Bitmap> imageService, BattleMapViewModel viewModel)
        {
            _unit = unit;
            _imageService = imageService;
            _viewModel = viewModel;

            _contrastingForegroundConverter = _resourcesLocator
                .TryFindResource("ContrastingForegroundConverter") as ContrastingForegroundConverter;

            IsHitTestVisible = false;
            Width = HexCoordinates.HexWidth;
            Height = HexCoordinates.HexHeight;

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

            // Create heat level bar (vertical)
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
            var selectionBorder = new Border
            {
                Width = Width * 0.9,
                Height = Height * 0.9,
                Opacity = 0.9,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                BorderBrush = new SolidColorBrush(color),
                BorderThickness = new Thickness(4),
                CornerRadius = new CornerRadius(Width / 2),
                IsVisible = false
            };

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

            Children.Add(selectionBorder);
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
                    UpdateSelectionBorder(state, selectionBorder);
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

            var activeUnit = _viewModel.CurrentState is WeaponsAttackState state
                ? state.Attacker
                : _viewModel.SelectedUnit;

            if (activeUnit != _unit) return;

            foreach (var action in actions)
            {
                if (!action.IsVisible) continue;

                var button = new Button
                {
                    Background = new SolidColorBrush(Colors.Aqua),
                    Padding = new Thickness(8, 4),
                    CornerRadius = new CornerRadius(4),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Content = new TextBlock
                    {
                        Text = action.Label,
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                };

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
            if (_unit.Position == null) return;
            var hexPosition = _unit.Position;

            var leftPos = hexPosition.Coordinates.H;
            var topPos = hexPosition.Coordinates.V;

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
                _ => _resourcesLocator.TryFindResource("DestroyedColor") as SolidColorBrush
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
            if (_resourcesLocator.TryFindResource("DamageLabelAnimation") is Animation animation)
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
            var image = await _imageService.GetImage("units/mechs", _unit.Model.ToUpper());
            if (image != null)
            {
                _unitImage.Source = image;
            }

            // Apply player's tint color if available
            if (_unit.Owner == null) return;
            var color = Color.Parse(_unit.Owner.Tint);
            _tintBorder.OpacityMask = new ImageBrush { Source = image, Stretch = Stretch.Fill };
            _tintBorder.Background = new SolidColorBrush(color);
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
            _subscription.Dispose();
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
                    Text = label.ToUpper(),
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
            if (_unitMovementPathSegments.Count > 0)
            {
                // Path already displayed, no need to recreate
                return;
            }

            if (Parent is not Canvas canvas) return;

            foreach (var segment in path.Segments)
            {
                var segmentViewModel = new PathSegmentViewModel(segment);
                var segmentControl = new PathSegmentControl(segmentViewModel, _viewModel);
                _unitMovementPathSegments.Add(segmentControl);
                canvas.Children.Add(segmentControl);
            }
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

        private void UpdateSelectionBorder(UnitState state, Border selectionBorder)
        {
            selectionBorder.IsVisible = state.SelectedUnit == _unit
                                        || _viewModel.CurrentState is WeaponsAttackState attackState &&
                                        (attackState.Attacker == _unit || attackState.SelectedTarget == _unit);
        }

        private void UpdateRotation(UnitState state)
        {
            var isMech = _unit is Mech;
            var torsoFacing = isMech && state.TorsoDirection.HasValue
                ? (int)state.TorsoDirection.Value
                : (int)state.Position!.Facing;

            // Rotate the entire control to show a torso/unit direction
            RenderTransform = new RotateTransform(torsoFacing * 60, 0, 0);
        }

        private void UpdateMovementPathDisplay(UnitState state)
        {
            // Show movement path when MovementTaken has cost, regardless of phase
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