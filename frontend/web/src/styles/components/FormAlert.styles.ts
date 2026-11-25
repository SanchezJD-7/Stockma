import Alert from '@mui/material/Alert'
import styled from '@emotion/styled'

export const AlertSurface = styled(Alert)`
  position: relative;
  overflow: hidden;
  border-radius: 8px;
  box-shadow: var(--color-shadow-sm);

  &.MuiAlert-colorSuccess {
    --alert-background: var(--color-tint-green);
    --alert-accent: var(--status-green);
    background: var(--color-tint-green);
    color: var(--status-green);
  }

  &.MuiAlert-colorError {
    --alert-background: var(--color-tint-red);
    --alert-accent: var(--status-red);
    background: var(--color-tint-red);
    color: var(--status-red);
  }

  &.MuiAlert-colorWarning {
    --alert-background: var(--color-tint-yellow);
    --alert-accent: var(--status-yellow);
    background: var(--color-tint-yellow);
    color: var(--status-yellow);
  }

  &.MuiAlert-colorInfo {
    --alert-background: var(--color-tint-accent);
    --alert-accent: var(--brand-teal);
    background: var(--color-tint-accent);
    color: var(--brand-teal);
  }

  .MuiAlert-icon,
  .MuiAlert-action {
    color: var(--alert-accent);
  }
`

export const AlertAccent = styled.span`
  position: absolute;
  right: 0;
  bottom: 0;
  left: 0;
  height: 3px;
  background: var(--alert-accent);
`
