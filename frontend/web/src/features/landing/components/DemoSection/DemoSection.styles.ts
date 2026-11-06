import styled from '@emotion/styled'
import { LandingSectionTitle } from '../../landing-texts'
import { MutedText } from '../../../../styles/components/texts'

export const DemoTitle = styled(LandingSectionTitle)`
  margin-bottom: 14px;
  font-size: clamp(28px, 4vw, 40px);
  letter-spacing: -0.02em;
  color: var(--color-white);
`

export const DemoBody = styled(MutedText)`
  max-width: 600px;
  margin: 0 auto 32px;
  font-size: 17px;
  color: rgba(255, 255, 255, 0.74);

  @media (max-width: 640px) {
    font-size: 15px;
  }
`
