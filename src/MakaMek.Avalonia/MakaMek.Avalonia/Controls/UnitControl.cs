using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media.Immutable;
using Sanet.MakaMek.Avalonia.Utils;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.UiStates;
using Sanet.MakaMek.Core.ViewModels;

namespace Sanet.MakaMek.Avalonia.Controls
{
    public class UnitControl : Grid, IDisposable
    {
        private readonly Image _unitImage;
        private readonly IImageService<Bitmap> _imageService;
        private readonly BattleMapViewModel _viewModel;
        private readonly Unit _unit;
        private readonly IDisposable _subscription;
        private readonly Border _tintBorder;
        private readonly StackPanel _actionButtons;
        private readonly StackPanel _healthBars;
        private readonly ProgressBar _armorBar;
        private readonly ProgressBar _structureBar;
        private readonly Path _destroyedCross;
        private readonly StackPanel _eventsPanel;
        private readonly TimeSpan _eventDisplayDuration = TimeSpan.FromSeconds(5);

        public UnitControl(Unit unit, IImageService<Bitmap> imageService, BattleMapViewModel viewModel)
        {
            _unit = unit;
            _imageService = imageService;
            _viewModel = viewModel;

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

            // Create health bars panel
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
            
            // Create events panel for damage labels
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
            var armorBrush = AvaloniaResourcesLocator.TryFindResource("MechArmorBrush") as SolidColorBrush 
                ?? new SolidColorBrush(Colors.LightBlue);
            var structureBrush = AvaloniaResourcesLocator.TryFindResource("MechStructureBrush") as SolidColorBrush 
                ?? new SolidColorBrush(Colors.Orange);
            var backgroundBrush = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0));

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

            // Add bars to the panel
            _healthBars.Children.Add(_armorBar);
            _healthBars.Children.Add(_structureBar);

            var color = _unit.Owner!=null 
                ?Color.Parse(_unit.Owner.Tint)
                :Colors.Yellow;
            var selectionBorder = new Border
            {
                Width = Width*0.9,
                Height = Height*0.9,
                Opacity = 0.9,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                BorderBrush = new SolidColorBrush(color),
                BorderThickness = new Thickness(4),
                CornerRadius = new CornerRadius(Width/2),
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

            // Create destroyed indicator (cross)
            _destroyedCross = new Path
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
            // Draw cross geometry
            var crossSize = _destroyedCross.Width;
            using (var context = ((StreamGeometry)_destroyedCross.Data).Open())
            {
                // Diagonal line 1
                context.BeginFigure(new Point(0, 0), true);
                context.LineTo(new Point(crossSize, crossSize));
                // Diagonal line 2
                context.BeginFigure(new Point(crossSize, 0), true);
                context.LineTo(new Point(0, crossSize));
            }

            Children.Add(selectionBorder);
            Children.Add(_unitImage);
            Children.Add(_tintBorder);
            Children.Add(torsoArrow);
            Children.Add(_destroyedCross);

            // Create an observable that polls the unit's position and selection state
            Observable
                .Interval(TimeSpan.FromMilliseconds(32)) // ~60fps
                .Select(_ => new
                {
                    _unit.Position,
                    _unit.IsDeployed,
                    viewModel.SelectedUnit,
                    Actions = viewModel.CurrentState.GetAvailableActions(),
                    IsWeaponsPhase = viewModel.CurrentState is WeaponsAttackState,
                    (_unit as Mech)?.TorsoDirection,
                    _unit.TotalMaxArmor,
                    _unit.TotalCurrentArmor,
                    _unit.TotalMaxStructure,
                    _unit.TotalCurrentStructure,
                    _unit.Events
                })
                .ObserveOn(SynchronizationContext.Current) // Ensure events are processed on the UI thread
                .Subscribe(state =>
                {
                    // Show/hide destroyed indicator
                    _destroyedCross.IsVisible = _unit.Status == UnitStatus.Destroyed;
                    _healthBars.IsVisible = _unit.Status != UnitStatus.Destroyed;
                    if (state.Position == null) return; // unit is not deployed, no need to display
                    
                    Render();
                    selectionBorder.IsVisible = state.SelectedUnit == _unit
                                                || _viewModel.CurrentState is WeaponsAttackState attackState && (attackState.Attacker == _unit || attackState.SelectedTarget == _unit);
                    UpdateActionButtons(state.Actions);
                    UpdateHealthBars(state.TotalCurrentArmor, state.TotalMaxArmor, state.TotalCurrentStructure, state.TotalMaxStructure);
                    
                    // Process any new events
                    if (state.Events.Any())
                    {
                        ProcessEvents(state.Events);
                    }
                    
                    // Calculate rotation angles
                    var isMech = _unit is Mech;

                    var torsoFacing = isMech && state.TorsoDirection.HasValue 
                        ? (int)state.TorsoDirection.Value 
                        : (int)state.Position!.Facing;
                    var unitFacing = (int)state.Position!.Facing;
                    
                    // Rotate entire control to show torso/unit direction
                    RenderTransform = new RotateTransform(torsoFacing * 60, 0, 0);

                    // Update direction indicator for mechs
                    if (isMech)
                    {
                        torsoArrow.IsVisible = state.IsWeaponsPhase 
                                               && _unit.Status != UnitStatus.Destroyed
                                               && state.TorsoDirection.HasValue 
                                               && state.Position!=null;
                        if (!torsoArrow.IsVisible) return;
                        // Since control is rotated to torso direction, we need opposite delta
                        var deltaAngle = (unitFacing - torsoFacing + 6) % 6 * 60;
                        torsoArrow.RenderTransform = new RotateTransform(deltaAngle);

                        // Check if torso direction has changed
                        if (!state.IsWeaponsPhase || !((Mech)_unit).HasUsedTorsoTwist) return;
                        if (state.TorsoDirection.HasValue)
                        {
                            (_viewModel.CurrentState as WeaponsAttackState)?.HandleTorsoRotation(_unit.Id);
                        }
                    }
                    else
                    {
                        torsoArrow.IsVisible = false;
                    }
                });
            
            // Initial update
            Render();
            UpdateImage();
            UpdateHealthBars(_unit.TotalCurrentArmor, _unit.TotalMaxArmor, _unit.TotalCurrentStructure, _unit.TotalMaxStructure);
        }

        private void UpdateHealthBars(int currentArmor, int maxArmor, int currentStructure, int maxStructure)
        {
            // Update armor bar
            _armorBar.Maximum = maxArmor;
            _armorBar.Value = currentArmor;
            
            // Update structure bar
            _structureBar.Maximum = maxStructure;
            _structureBar.Value = currentStructure;
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
            }
            Canvas.SetLeft(_healthBars, leftPos);
            Canvas.SetTop(_healthBars, topPos - 15); // Position above the unit
            
            // Update events panel position to follow the unit
            if (_eventsPanel.Parent == null && Parent is Canvas canvas3)
            {
                canvas3.Children.Add(_eventsPanel);
            }
            Canvas.SetLeft(_eventsPanel, leftPos);
            Canvas.SetTop(_eventsPanel, topPos - 40); // Position above the health bars
        }
        
        /// <summary>
        /// Processes events from the unit and creates UI elements to display them
        /// </summary>
        /// <param name="events">The events to process</param>
        private void ProcessEvents(IReadOnlyCollection<UiEvent> events)
        {
            if (events.Count == 0) return;
            
            // Process all events in the queue
            while (_unit.DequeueEvent() is { } uiEvent)
            {
                string template = _viewModel.LocalizationService.GetString($"Events_Unit_{uiEvent.Type}");
                var damageText = string.Format(template, uiEvent.Parameters);
                CreateDamageLabel(damageText);         
            }
        }
        
        /// <summary>
        /// Creates a damage label with animation
        /// </summary>
        /// <param name="damageText">The damage text to display</param>
        private void CreateDamageLabel(string damageText)
        {
            // Create a border with text
            var textBlock = new TextBlock
            {
                Text = damageText,
                Foreground = new ImmutableSolidColorBrush(Colors.Red),
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            var border = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2),
                Background = new ImmutableSolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                Child = textBlock
            };
            
            // Add to the events panel
            _eventsPanel.Children.Add(border);
            
            // Start fade out animation
            _ = AnimateDamageLabelAsync(border);
        }
        
        /// <summary>
        /// Animates a damage label to fade out and move up
        /// </summary>
        /// <param name="label">The label to animate</param>
        private async Task AnimateDamageLabelAsync(Border label)
        {
            var startTime = DateTime.Now;
            var endTime = startTime + _eventDisplayDuration;
            
            // Initial position offset
            var initialOffset = 0.0;
            var targetOffset = -20.0; // Move up by 20 pixels
            
            while (DateTime.Now < endTime)
            {
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                var duration = _eventDisplayDuration.TotalMilliseconds;
                var progress = Math.Min(elapsed / duration, 1.0);
                
                // Calculate current opacity and position
                var currentOpacity = 1.0 - progress;
                var currentOffset = initialOffset + (targetOffset - initialOffset) * progress;
                
                // Apply opacity and transform
                label.Opacity = currentOpacity;
                label.RenderTransform = new TranslateTransform(0, currentOffset);
                
                // Wait a bit before the next frame
                await Task.Delay(16); // ~60fps
            }
            
            // Remove the label from the panel
            _eventsPanel.Children.Remove(label);
        }
        
        private void UpdateImage()
        {
            var image = _imageService.GetImage("units/mechs", _unit.Model.ToUpper());
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

            if (clickedButton is not {IsEnabled:true, IsVisible:true}) return false;
            // Trigger the button's Click event
            clickedButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            return true;
        }

        public void Dispose()
        {
            _subscription.Dispose();
        }
        
        public StackPanel ActionButtons => _actionButtons;
    }
}
