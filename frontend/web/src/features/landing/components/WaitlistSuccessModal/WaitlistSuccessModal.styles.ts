import { keyframes } from '@emotion/react'
import styled from '@emotion/styled'
import { MutedText } from '../../../../styles/components/texts.styles'

const popIn = keyframes`
  0% {
    opacity: 0;
    transform: scale(0.65);
  }

  70% {
    opacity: 1;
    transform: scale(1.08);
  }

  100% {
    opacity: 1;
    transform: scale(1);
  }
`

const drawCheck = keyframes`
  to {
    stroke-dashoffset: 0;
  }
`

export const SuccessIcon = styled.svg`
  display: block;
  width: 64px;
  height: 64px;
  margin: 20px auto;
  overflow: visible;
  color: var(--status-green);

  circle {
    transform-box: fill-box;
    transform-origin: center;
    animation: ${popIn} 420ms cubic-bezier(0.2, 0.8, 0.2, 1) both;
  }

  path {
    stroke-dasharray: 40;
    stroke-dashoffset: 40;
    animation: ${drawCheck} 280ms 180ms ease-out forwards;
  }

  @media (prefers-reduced-motion: reduce) {
    circle,
    path {
      animation: none;
    }

    path {
      stroke-dashoffset: 0;
    }
  }
`

export const SuccessBody = styled(MutedText)`
  margin: 10px 0;
  font-size: var(--fs-14);
  line-height: 1.6;
`
